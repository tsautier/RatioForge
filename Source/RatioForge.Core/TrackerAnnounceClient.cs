namespace RatioForge;

using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using BitTorrent;

/// <summary>Options for one tracker announce request.</summary>
public sealed record TrackerAnnounceOptions(
    TorrentDocument Torrent,
    ClientProfile Profile,
    long Uploaded,
    long Downloaded,
    int Port,
    int PeerCount,
    string Event = "started",
    string LocalIp = "",
    TrackerProxyOptions? Proxy = null,
    ClientIdentity? Identity = null);

public enum TrackerProxyMode
{
    None,
    System,
    Http,
    Socks5,
}

/// <summary>Proxy configuration for tracker HTTP and HTTPS requests.</summary>
public sealed record TrackerProxyOptions(
    TrackerProxyMode Mode,
    string Host = "",
    int Port = 8080,
    string Username = "",
    string Password = "");

/// <summary>Result returned by a BitTorrent tracker.</summary>
public sealed record TrackerAnnounceResult(
    bool IsSuccess,
    string Message,
    int? IntervalSeconds,
    string RequestUrl);

/// <summary>Sends HTTP and HTTPS tracker announce requests.</summary>
public sealed class TrackerAnnounceClient : IDisposable
{
    private readonly HttpClient? injectedHttpClient;

    public TrackerAnnounceClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            injectedHttpClient = httpClient;
        }
    }

    /// <summary>Sends an announce and parses the tracker's bencoded status.</summary>
    public async Task<TrackerAnnounceResult> AnnounceAsync(
        TrackerAnnounceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be between 1 and 65535.");
        }

        IPAddress? localAddress = NetworkAddressCatalog.ParseOptional(options.LocalIp);
        TorrentClient client = options.Profile.CreateClient();
        ClientIdentity identity = options.Identity ?? new ClientIdentity(client.Key, client.PeerID);
        long left = Math.Max(0, options.Torrent.TotalSize - options.Downloaded);
        var info = new TorrentInfo(options.Uploaded, options.Downloaded)
        {
            tracker = options.Torrent.Tracker,
            hash = options.Torrent.InfoHash,
            left = left,
            totalsize = options.Torrent.TotalSize,
            peerID = identity.PeerId,
            port = options.Port.ToString(CultureInfo.InvariantCulture),
            key = identity.Key,
            numberOfPeers = options.PeerCount.ToString(CultureInfo.InvariantCulture),
        };

        string trackerEvent = string.IsNullOrWhiteSpace(options.Event)
            ? string.Empty
            : "&event=" + options.Event.Trim().ToLowerInvariant();
        string requestUrl = TrackerUrlBuilder.BuildAnnounce(info, client, trackerEvent, localAddress?.ToString() ?? string.Empty);
        var uri = new Uri(requestUrl, UriKind.Absolute);
        if (uri.Scheme is not ("http" or "https"))
        {
            throw new NotSupportedException("The cross-platform client currently supports HTTP and HTTPS trackers.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Profile.UserAgent);
        using HttpClient? ownedClient = injectedHttpClient is null
            ? CreateHttpClient(localAddress, proxy: options.Proxy)
            : null;
        HttpClient sender = injectedHttpClient ?? ownedClient!;
        using HttpResponseMessage response = await sender.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        IBEncodeValue value = BEncode.Parse(body);
        if (value is not ValueDictionary dictionary)
        {
            return new TrackerAnnounceResult(false, "Invalid tracker response.", null, requestUrl);
        }

        if (dictionary.Contains("failure reason"))
        {
            return new TrackerAnnounceResult(
                false,
                BEncode.String(dictionary["failure reason"]) ?? "Tracker rejected the announce.",
                null,
                requestUrl);
        }

        int? interval = dictionary.Contains("interval") && dictionary["interval"] is ValueNumber number
            ? checked((int)number.Integer)
            : null;
        string message = dictionary.Contains("warning message")
            ? BEncode.String(dictionary["warning message"]) ?? "Tracker returned a warning."
            : "Announce accepted.";
        return new TrackerAnnounceResult(true, message, interval, requestUrl);
    }

    public void Dispose()
    {
        // Injected HttpClient instances remain owned by their caller.
    }

    internal static HttpClient CreateHttpClient(
        IPAddress? localAddress,
        RemoteCertificateValidationCallback? certificateValidation = null,
        TrackerProxyOptions? proxy = null)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        if (certificateValidation is not null)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = certificateValidation;
        }

        ConfigureProxy(handler, proxy);
        if (localAddress is not null)
        {
            handler.ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(localAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                try
                {
                    socket.Bind(new IPEndPoint(localAddress, 0));
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };
        }

        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static void ConfigureProxy(SocketsHttpHandler handler, TrackerProxyOptions? options)
    {
        if (options is null || options.Mode == TrackerProxyMode.System)
        {
            return;
        }

        if (options.Mode == TrackerProxyMode.None)
        {
            handler.UseProxy = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Host) || options.Port is < 1 or > 65535)
        {
            throw new ArgumentException("A proxy host and a port between 1 and 65535 are required.", nameof(options));
        }

        string scheme = options.Mode == TrackerProxyMode.Socks5 ? "socks5" : "http";
        var proxy = new WebProxy(new UriBuilder(scheme, options.Host.Trim(), options.Port).Uri);
        if (!string.IsNullOrEmpty(options.Username))
        {
            proxy.Credentials = new NetworkCredential(options.Username, options.Password);
        }

        handler.UseProxy = true;
        handler.Proxy = proxy;
    }
}
