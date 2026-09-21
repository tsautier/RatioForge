namespace RatioForge;

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

/// <summary>Implements the BitTorrent UDP tracker protocol defined by BEP 15.</summary>
internal static class UdpTrackerClient
{
    private const long ProtocolConnectionId = 0x41727101980;
    private const int ConnectAction = 0;
    private const int AnnounceAction = 1;

    public static async Task<TrackerAnnounceResult> AnnounceAsync(
        TrackerAnnounceOptions options,
        string trackerUrl,
        CancellationToken cancellationToken)
    {
        if (options.Proxy is { Mode: not (TrackerProxyMode.None or TrackerProxyMode.System) })
        {
            throw new NotSupportedException("HTTP and SOCKS proxies cannot relay UDP tracker traffic.");
        }

        var uri = new Uri(trackerUrl, UriKind.Absolute);
        IPEndPoint remote = await ResolveAsync(uri, options.LocalIp, cancellationToken).ConfigureAwait(false);
        IPAddress? localAddress = NetworkAddressCatalog.ParseOptional(options.LocalIp);
        using var udp = new UdpClient(remote.AddressFamily);
        if (localAddress is not null)
        {
            udp.Client.Bind(new IPEndPoint(localAddress, 0));
        }

        udp.Connect(remote);
        long started = Stopwatch.GetTimestamp();
        long connectionId = await ConnectAsync(udp, cancellationToken).ConfigureAwait(false);
        byte[] request = BuildAnnounceRequest(options, connectionId, out int transactionId);
        await udp.SendAsync(request, cancellationToken).ConfigureAwait(false);
        UdpReceiveResult received = await ReceiveAsync(udp, cancellationToken).ConfigureAwait(false);
        long elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        return ParseAnnounceResponse(received.Buffer, transactionId, trackerUrl, elapsed, remote.AddressFamily);
    }

    public static async Task<long> TestConnectionAsync(
        string trackerUrl,
        string localIp,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(trackerUrl, UriKind.Absolute);
        IPEndPoint remote = await ResolveAsync(uri, localIp, cancellationToken).ConfigureAwait(false);
        IPAddress? localAddress = NetworkAddressCatalog.ParseOptional(localIp);
        using var udp = new UdpClient(remote.AddressFamily);
        if (localAddress is not null)
        {
            udp.Client.Bind(new IPEndPoint(localAddress, 0));
        }

        udp.Connect(remote);
        long started = Stopwatch.GetTimestamp();
        _ = await ConnectAsync(udp, cancellationToken).ConfigureAwait(false);
        return (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    internal static byte[] BuildAnnounceRequest(
        TrackerAnnounceOptions options,
        long connectionId,
        out int transactionId)
    {
        byte[] infoHash = Convert.FromHexString(options.Torrent.InfoHash);
        ClientIdentity identity = options.Identity ?? options.Profile.CreateIdentity();
        byte[] peerId = DecodePeerId(identity.PeerId);
        if (infoHash.Length != 20 || peerId.Length != 20)
        {
            throw new InvalidDataException("UDP announces require 20-byte info hashes and peer IDs.");
        }

        transactionId = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        long left = options.Left ?? Math.Max(0, options.Torrent.TotalSize - options.Downloaded);
        var packet = new byte[98];
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(0, 8), connectionId);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8, 4), AnnounceAction);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(12, 4), transactionId);
        infoHash.CopyTo(packet, 16);
        peerId.CopyTo(packet, 36);
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(56, 8), options.Downloaded);
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(64, 8), left);
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(72, 8), options.Uploaded);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(80, 4), EventCode(options.Event));
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(84, 4), 0);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(88, 4), StableKey(identity.Key));
        BinaryPrimitives.WriteInt32BigEndian(
            packet.AsSpan(92, 4),
            options.Event.Equals("stopped", StringComparison.OrdinalIgnoreCase) ? 0 : options.PeerCount);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(96, 2), checked((ushort)options.Port));
        return packet;
    }

    internal static TrackerAnnounceResult ParseAnnounceResponse(
        byte[] response,
        int transactionId,
        string trackerUrl,
        long elapsedMilliseconds,
        AddressFamily addressFamily)
    {
        if (response.Length < 8)
        {
            throw new InvalidDataException("The UDP tracker returned a truncated response.");
        }

        int action = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(0, 4));
        int receivedTransaction = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(4, 4));
        if (receivedTransaction != transactionId)
        {
            throw new InvalidDataException("The UDP tracker returned a mismatched transaction ID.");
        }

        if (action == 3)
        {
            string error = Encoding.UTF8.GetString(response, 8, response.Length - 8);
            return new TrackerAnnounceResult(false, error, null, trackerUrl,
                HttpVersion: "UDP", ElapsedMilliseconds: elapsedMilliseconds,
                Server: new Uri(trackerUrl).Host, FinalUrl: trackerUrl);
        }

        if (action != AnnounceAction || response.Length < 20)
        {
            throw new InvalidDataException("The UDP tracker returned an invalid announce response.");
        }

        int interval = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(8, 4));
        int incomplete = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(12, 4));
        int complete = BinaryPrimitives.ReadInt32BigEndian(response.AsSpan(16, 4));
        int peerSize = addressFamily == AddressFamily.InterNetworkV6 ? 18 : 6;
        int peers = (response.Length - 20) / peerSize;
        return new TrackerAnnounceResult(true, "Announce accepted.", interval, trackerUrl,
            HttpVersion: "UDP", ElapsedMilliseconds: elapsedMilliseconds,
            Server: new Uri(trackerUrl).Host, FinalUrl: trackerUrl,
            Complete: complete, Incomplete: incomplete,
            Ipv4Peers: addressFamily == AddressFamily.InterNetwork ? peers : null,
            Ipv6Peers: addressFamily == AddressFamily.InterNetworkV6 ? peers : null);
    }

    private static async Task<long> ConnectAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        int transactionId = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        var request = new byte[16];
        BinaryPrimitives.WriteInt64BigEndian(request.AsSpan(0, 8), ProtocolConnectionId);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(8, 4), ConnectAction);
        BinaryPrimitives.WriteInt32BigEndian(request.AsSpan(12, 4), transactionId);
        await udp.SendAsync(request, cancellationToken).ConfigureAwait(false);
        UdpReceiveResult received = await ReceiveAsync(udp, cancellationToken).ConfigureAwait(false);
        if (received.Buffer.Length < 16 ||
            BinaryPrimitives.ReadInt32BigEndian(received.Buffer.AsSpan(0, 4)) != ConnectAction ||
            BinaryPrimitives.ReadInt32BigEndian(received.Buffer.AsSpan(4, 4)) != transactionId)
        {
            throw new InvalidDataException("The UDP tracker returned an invalid connection response.");
        }

        return BinaryPrimitives.ReadInt64BigEndian(received.Buffer.AsSpan(8, 8));
    }

    private static async Task<UdpReceiveResult> ReceiveAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        return await udp.ReceiveAsync(timeout.Token).ConfigureAwait(false);
    }

    private static async Task<IPEndPoint> ResolveAsync(Uri uri, string localIp, CancellationToken cancellationToken)
    {
        int port = uri.IsDefaultPort ? 80 : uri.Port;
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
        IPAddress? local = NetworkAddressCatalog.ParseOptional(localIp);
        IPAddress? selected = addresses.FirstOrDefault(address => local is null || address.AddressFamily == local.AddressFamily);
        return selected is null
            ? throw new SocketException((int)SocketError.HostNotFound)
            : new IPEndPoint(selected, port);
    }

    private static int EventCode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "completed" => 1,
        "started" => 2,
        "stopped" => 3,
        _ => 0,
    };

    private static int StableKey(string key)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4));
    }

    internal static byte[] DecodePeerId(string peerId)
    {
        var bytes = new List<byte>(20);
        for (int index = 0; index < peerId.Length; index++)
        {
            if (peerId[index] == '%' && index + 2 < peerId.Length &&
                byte.TryParse(peerId.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte escaped))
            {
                bytes.Add(escaped);
                index += 2;
            }
            else if (peerId[index] <= byte.MaxValue)
            {
                bytes.Add((byte)peerId[index]);
            }
            else
            {
                throw new InvalidDataException("The peer ID contains a character that cannot be encoded as one byte.");
            }
        }

        return bytes.ToArray();
    }
}
