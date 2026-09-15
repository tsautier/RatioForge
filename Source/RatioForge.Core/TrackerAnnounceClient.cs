namespace RatioForge;

using System.Globalization;
using System.Diagnostics;
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
    ClientIdentity? Identity = null,
    long? Left = null,
    string? TrackerUrl = null);

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
    string RequestUrl,
    int? HttpStatusCode = null,
    string HttpVersion = "",
    long ElapsedMilliseconds = 0,
    string Server = "",
    string ContentType = "",
    long? ContentLength = null,
    string FinalUrl = "",
    int? Complete = null,
    int? Incomplete = null,
    int? MinimumIntervalSeconds = null,
    int? Ipv4Peers = null,
    int? Ipv6Peers = null);

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

        string trackerUrl = options.TrackerUrl ?? options.Torrent.Tracker;
        var trackerUri = new Uri(trackerUrl, UriKind.Absolute);
        if (trackerUri.Scheme == "udp")
        {
            return await UdpTrackerClient.AnnounceAsync(options, trackerUrl, cancellationToken).ConfigureAwait(false);
        }

        if (trackerUri.Scheme is not ("http" or "https"))
        {
            throw new NotSupportedException($"Tracker protocol '{trackerUri.Scheme}' is not supported.");
        }

        IPAddress? localAddress = NetworkAddressCatalog.ParseOptional(options.LocalIp);
        TorrentClient client = options.Profile.CreateClient();
        ClientIdentity identity = options.Identity ?? new ClientIdentity(client.Key, client.PeerID);
        long left = options.Left ?? Math.Max(0, options.Torrent.TotalSize - options.Downloaded);
        if (left is < 0 || left > options.Torrent.TotalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Left must be between zero and the torrent size.");
        }

        var info = new TorrentInfo(options.Uploaded, options.Downloaded)
        {
            tracker = trackerUrl,
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

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Profile.UserAgent);
        using HttpClient? ownedClient = injectedHttpClient is null
            ? CreateHttpClient(localAddress, proxy: options.Proxy)
            : null;
        HttpClient sender = injectedHttpClient ?? ownedClient!;
        long requestStarted = Stopwatch.GetTimestamp();
        using HttpResponseMessage response = await sender.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        long elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds;
        int statusCode = (int)response.StatusCode;
        string httpVersion = $"HTTP/{response.Version}";
        string server = response.Headers.Server.ToString();
        string contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        long? contentLength = response.Content.Headers.ContentLength;
        string finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? requestUrl;
        if (!response.IsSuccessStatusCode)
        {
            return new TrackerAnnounceResult(
                false,
                $"HTTP {statusCode} {response.ReasonPhrase}".Trim(),
                null,
                requestUrl,
                statusCode,
                httpVersion,
                elapsedMilliseconds,
                server,
                contentType,
                contentLength,
                finalUrl);
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        IBEncodeValue value = BEncode.Parse(body);
        if (value is not ValueDictionary dictionary)
        {
            return new TrackerAnnounceResult(
                false,
                "Invalid tracker response.",
                null,
                requestUrl,
                statusCode,
                httpVersion,
                elapsedMilliseconds,
                server,
                contentType,
                contentLength,
                finalUrl);
        }

        int? interval = ReadOptionalInt(dictionary, "interval");
        int? complete = ReadOptionalInt(dictionary, "complete");
        int? incomplete = ReadOptionalInt(dictionary, "incomplete");
        int? minimumInterval = ReadOptionalInt(dictionary, "min interval");
        int? ipv4Peers = ReadPeerCount(dictionary, "peers", 6);
        int? ipv6Peers = ReadPeerCount(dictionary, "peers6", 18);
        if (dictionary.Contains("failure reason"))
        {
            return new TrackerAnnounceResult(
                false,
                BEncode.String(dictionary["failure reason"]) ?? "Tracker rejected the announce.",
                interval,
                requestUrl,
                statusCode,
                httpVersion,
                elapsedMilliseconds,
                server,
                contentType,
                contentLength,
                finalUrl,
                complete,
                incomplete,
                minimumInterval,
                ipv4Peers,
                ipv6Peers);
        }

        string message = dictionary.Contains("warning message")
            ? BEncode.String(dictionary["warning message"]) ?? "Tracker returned a warning."
            : "Announce accepted.";
        return new TrackerAnnounceResult(
            true,
            message,
            interval,
            requestUrl,
            statusCode,
            httpVersion,
            elapsedMilliseconds,
            server,
            contentType,
            contentLength,
            finalUrl,
            complete,
            incomplete,
            minimumInterval,
            ipv4Peers,
            ipv6Peers);
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

    private static int? ReadOptionalInt(ValueDictionary dictionary, string key) =>
        dictionary.Contains(key) && dictionary[key] is ValueNumber number
            ? checked((int)number.Integer)
            : null;

    private static int? ReadPeerCount(ValueDictionary dictionary, string key, int compactPeerSize) =>
        dictionary.Contains(key) ? dictionary[key] switch
        {
            ValueString compact => compact.Length / compactPeerSize,
            ValueList list => list.Values.Count,
            _ => null,
        } : null;
}
