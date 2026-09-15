namespace RatioForge;

using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

public enum NetworkDiagnosticStatus
{
    Passed,
    Warning,
    Failed,
}

/// <summary>One readable result from a tracker connectivity diagnostic.</summary>
public sealed record NetworkDiagnosticResult(
    string Test,
    NetworkDiagnosticStatus Status,
    string Details,
    long ElapsedMilliseconds = 0);

/// <summary>Checks DNS, address families, transport security, and configured proxy connectivity.</summary>
public sealed class NetworkDiagnosticsService
{
    public async Task<IReadOnlyList<NetworkDiagnosticResult>> RunAsync(
        string trackerUrl,
        string localIp = "",
        TrackerProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<NetworkDiagnosticResult>();
        if (!Uri.TryCreate(trackerUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https" or "udp"))
        {
            return [new("Tracker URL", NetworkDiagnosticStatus.Failed, "The tracker URL or protocol is invalid.")];
        }

        IPAddress[] addresses;
        long dnsStarted = Stopwatch.GetTimestamp();
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
            results.Add(new("DNS", NetworkDiagnosticStatus.Passed,
                $"Resolved {uri.Host} to {addresses.Length} address(es).",
                Elapsed(dnsStarted)));
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            results.Add(new("DNS", NetworkDiagnosticStatus.Failed, exception.Message, Elapsed(dnsStarted)));
            addresses = [];
        }

        AddAddressFamilyResult(results, addresses, AddressFamily.InterNetwork, "IPv4");
        AddAddressFamilyResult(results, addresses, AddressFamily.InterNetworkV6, "IPv6");

        if (proxy is { Mode: TrackerProxyMode.Http or TrackerProxyMode.Socks5 })
        {
            await TestTcpAsync(results, "Proxy", proxy.Host, proxy.Port, null, cancellationToken).ConfigureAwait(false);
            if (uri.Scheme is "http" or "https")
            {
                await TestHttpRouteAsync(results, uri, localIp, proxy, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                results.Add(new("Tracker transport", NetworkDiagnosticStatus.Failed,
                    "Configured HTTP/SOCKS proxies cannot relay UDP tracker datagrams."));
            }

            return results;
        }

        if (proxy is { Mode: TrackerProxyMode.System } && uri.Scheme is "http" or "https")
        {
            await TestHttpRouteAsync(results, uri, localIp, proxy, cancellationToken).ConfigureAwait(false);
            return results;
        }

        if (uri.Scheme == "udp")
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                long latency = await UdpTrackerClient.TestConnectionAsync(trackerUrl, localIp, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new("UDP", NetworkDiagnosticStatus.Passed,
                    "The tracker accepted a BEP 15 connection handshake.", latency));
            }
            catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
            {
                results.Add(new("UDP", NetworkDiagnosticStatus.Failed, exception.Message, Elapsed(started)));
            }

            return results;
        }

        int port = uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port;
        await TestTcpAsync(results, "TCP", uri.Host, port, localIp, cancellationToken).ConfigureAwait(false);
        if (uri.Scheme == "https")
        {
            await TestTlsAsync(results, uri, localIp, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            results.Add(new("TLS", NetworkDiagnosticStatus.Warning, "The tracker uses unencrypted HTTP."));
        }

        return results;
    }

    private static async Task TestHttpRouteAsync(
        ICollection<NetworkDiagnosticResult> results,
        Uri uri,
        string localIp,
        TrackerProxyOptions proxy,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            using HttpClient client = TrackerAnnounceClient.CreateHttpClient(
                NetworkAddressCatalog.ParseOptional(localIp), proxy: proxy);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using HttpResponseMessage response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            results.Add(new("Proxy route", NetworkDiagnosticStatus.Passed,
                $"Tracker route responded with HTTP {(int)response.StatusCode}.", Elapsed(started)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            results.Add(new("Proxy route", NetworkDiagnosticStatus.Failed, exception.Message, Elapsed(started)));
        }
    }

    private static async Task TestTcpAsync(
        ICollection<NetworkDiagnosticResult> results,
        string name,
        string host,
        int port,
        string? localIp,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            using Socket socket = await ConnectAsync(host, port, localIp, cancellationToken).ConfigureAwait(false);
            results.Add(new(name, NetworkDiagnosticStatus.Passed,
                $"Connected to {host}:{port}.", Elapsed(started)));
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            results.Add(new(name, NetworkDiagnosticStatus.Failed, exception.Message, Elapsed(started)));
        }
    }

    private static async Task TestTlsAsync(
        ICollection<NetworkDiagnosticResult> results,
        Uri uri,
        string localIp,
        CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            int port = uri.IsDefaultPort ? 443 : uri.Port;
            using Socket socket = await ConnectAsync(uri.Host, port, localIp, cancellationToken).ConfigureAwait(false);
            await using var stream = new NetworkStream(socket, ownsSocket: false);
            await using var tls = new SslStream(stream, leaveInnerStreamOpen: false);
            await tls.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions { TargetHost = uri.Host },
                cancellationToken).ConfigureAwait(false);
            results.Add(new("TLS", NetworkDiagnosticStatus.Passed,
                $"Negotiated {tls.SslProtocol}; certificate validation passed.", Elapsed(started)));
        }
        catch (Exception exception) when (exception is SocketException or IOException or AuthenticationException or OperationCanceledException)
        {
            results.Add(new("TLS", NetworkDiagnosticStatus.Failed, exception.Message, Elapsed(started)));
        }
    }

    private static async Task<Socket> ConnectAsync(
        string host,
        int port,
        string? localIp,
        CancellationToken cancellationToken)
    {
        IPAddress? local = NetworkAddressCatalog.ParseOptional(localIp);
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        IPAddress remote = addresses.FirstOrDefault(address => local is null || address.AddressFamily == local.AddressFamily)
            ?? throw new SocketException((int)SocketError.AddressFamilyNotSupported);
        var socket = new Socket(remote.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            if (local is not null)
            {
                socket.Bind(new IPEndPoint(local, 0));
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await socket.ConnectAsync(new IPEndPoint(remote, port), timeout.Token).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static void AddAddressFamilyResult(
        ICollection<NetworkDiagnosticResult> results,
        IEnumerable<IPAddress> addresses,
        AddressFamily family,
        string name)
    {
        int count = addresses.Count(address => address.AddressFamily == family);
        results.Add(new(name,
            count > 0 ? NetworkDiagnosticStatus.Passed : NetworkDiagnosticStatus.Warning,
            count > 0 ? $"{count} {name} address(es) available." : $"No {name} address was returned by DNS."));
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
