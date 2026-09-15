namespace RatioForge.Tests;

using System.Buffers.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public sealed class RoadmapFeaturesTests
{
    [Test]
    public void TorrentDocumentShouldPreserveAnnounceListTiers()
    {
        TorrentDocument document = TorrentDocument.Load(FixturePath("announce-list.torrent"));

        Assert.Multiple(() =>
        {
            Assert.That(document.TrackerTiers, Has.Count.EqualTo(2));
            Assert.That(document.Trackers, Is.EqualTo(new[]
            {
                "https://tracker.example/announce",
                "udp://tracker2.example:6969",
            }));
        });
    }

    [Test]
    public void UdpAnnouncePacketShouldFollowBep15Layout()
    {
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent"));
        var identity = new ClientIdentity("fixed-key", "-qB5230-123456789012");
        var options = new TrackerAnnounceOptions(
            torrent, ClientProfileCatalog.Default, 300, 100, 6881, 50,
            "completed", Identity: identity, Left: 200);

        byte[] packet = UdpTrackerClient.BuildAnnounceRequest(options, 42, out int transactionId);

        Assert.Multiple(() =>
        {
            Assert.That(packet, Has.Length.EqualTo(98));
            Assert.That(BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(0, 8)), Is.EqualTo(42));
            Assert.That(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(8, 4)), Is.EqualTo(1));
            Assert.That(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(12, 4)), Is.EqualTo(transactionId));
            Assert.That(BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(56, 8)), Is.EqualTo(100));
            Assert.That(BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(64, 8)), Is.EqualTo(200));
            Assert.That(BinaryPrimitives.ReadInt64BigEndian(packet.AsSpan(72, 8)), Is.EqualTo(300));
            Assert.That(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(80, 4)), Is.EqualTo(1));
            Assert.That(BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(92, 4)), Is.EqualTo(50));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(96, 2)), Is.EqualTo(6881));
        });
    }

    [Test]
    public void UdpAnnounceResponseShouldExposeTrackerStatistics()
    {
        const int transactionId = 123;
        var response = new byte[26];
        BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(4, 4), transactionId);
        BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(8, 4), 900);
        BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(12, 4), 7);
        BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(16, 4), 11);

        TrackerAnnounceResult result = UdpTrackerClient.ParseAnnounceResponse(
            response, transactionId, "udp://tracker.example:6969", 12, AddressFamily.InterNetwork);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.HttpVersion, Is.EqualTo("UDP"));
            Assert.That(result.IntervalSeconds, Is.EqualTo(900));
            Assert.That(result.Incomplete, Is.EqualTo(7));
            Assert.That(result.Complete, Is.EqualTo(11));
            Assert.That(result.Ipv4Peers, Is.EqualTo(1));
        });
    }

    [Test]
    public void UdpPeerIdShouldDecodeLegacyHttpEscapes()
    {
        byte[] peerId = UdpTrackerClient.DecodePeerId("-UT3320-%18w1234567890");

        Assert.Multiple(() =>
        {
            Assert.That(peerId, Has.Length.EqualTo(20));
            Assert.That(peerId[8], Is.EqualTo(0x18));
            Assert.That(peerId[9], Is.EqualTo((byte)'w'));
        });
    }

    [Test]
    public async Task AnnounceClientShouldCompleteUdpHandshakeAndAnnounce()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        const long connectionId = 0x1020304050607080;
        Task responder = Task.Run(async () =>
        {
            UdpReceiveResult connect = await server.ReceiveAsync();
            int connectTransaction = BinaryPrimitives.ReadInt32BigEndian(connect.Buffer.AsSpan(12, 4));
            var connectResponse = new byte[16];
            BinaryPrimitives.WriteInt32BigEndian(connectResponse.AsSpan(0, 4), 0);
            BinaryPrimitives.WriteInt32BigEndian(connectResponse.AsSpan(4, 4), connectTransaction);
            BinaryPrimitives.WriteInt64BigEndian(connectResponse.AsSpan(8, 8), connectionId);
            await server.SendAsync(connectResponse, connect.RemoteEndPoint);

            UdpReceiveResult announce = await server.ReceiveAsync();
            int announceTransaction = BinaryPrimitives.ReadInt32BigEndian(announce.Buffer.AsSpan(12, 4));
            var announceResponse = new byte[20];
            BinaryPrimitives.WriteInt32BigEndian(announceResponse.AsSpan(0, 4), 1);
            BinaryPrimitives.WriteInt32BigEndian(announceResponse.AsSpan(4, 4), announceTransaction);
            BinaryPrimitives.WriteInt32BigEndian(announceResponse.AsSpan(8, 4), 600);
            BinaryPrimitives.WriteInt32BigEndian(announceResponse.AsSpan(12, 4), 4);
            BinaryPrimitives.WriteInt32BigEndian(announceResponse.AsSpan(16, 4), 9);
            await server.SendAsync(announceResponse, announce.RemoteEndPoint);
        });
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = $"udp://127.0.0.1:{port}",
        };
        using var client = new TrackerAnnounceClient();

        TrackerAnnounceResult result = await client.AnnounceAsync(new TrackerAnnounceOptions(
            torrent, ClientProfileCatalog.Default, 10, 20, 6881, 50,
            Identity: new ClientIdentity("key", "-qB5230-123456789012"), Left: 30));
        await responder;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IntervalSeconds, Is.EqualTo(600));
            Assert.That(result.Complete, Is.EqualTo(9));
            Assert.That(result.Incomplete, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task AnnounceClientShouldUseSelectedTrackerCandidate()
    {
        var handler = new CapturingHandler("d8:intervali900e5:peers0:e");
        using var http = new HttpClient(handler);
        using var client = new TrackerAnnounceClient(http);
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent"));

        TrackerAnnounceResult result = await client.AnnounceAsync(new TrackerAnnounceOptions(
            torrent, ClientProfileCatalog.Default, 0, 0, 6881, 50,
            TrackerUrl: "https://secondary.example/announce"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(handler.RequestUri, Does.StartWith("https://secondary.example/announce?"));
        });
    }

    [Test]
    public async Task InvalidTrackerShouldProduceReadableNetworkDiagnostic()
    {
        var service = new NetworkDiagnosticsService();

        IReadOnlyList<NetworkDiagnosticResult> results = await service.RunAsync("file:///tracker");

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Test, Is.EqualTo("Tracker URL"));
            Assert.That(results[0].Status, Is.EqualTo(NetworkDiagnosticStatus.Failed));
        });
    }

    [Test]
    public void SessionProfilesShouldRoundTripAndApply()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-profiles-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new ApplicationSettings
            {
                DefaultProfileName = "Transmission 4.1.3",
                UploadRateKib = 77,
                LocalAddress = "127.0.0.1",
                ProxyMode = TrackerProxyMode.Socks5,
                ProxyHost = "proxy.example",
                ProxyPassword = "must-not-persist",
            };
            SessionProfileStore.Save([SessionProfile.FromSettings("Seed box", settings)], path);

            SessionProfile profile = SessionProfileStore.Load(path).Single();
            var applied = new ApplicationSettings();
            profile.ApplyTo(applied);

            Assert.Multiple(() =>
            {
                Assert.That(profile.Name, Is.EqualTo("Seed box"));
                Assert.That(applied.DefaultProfileName, Is.EqualTo("Transmission 4.1.3"));
                Assert.That(applied.UploadRateKib, Is.EqualTo(77));
                Assert.That(applied.ProxyMode, Is.EqualTo(TrackerProxyMode.Socks5));
                Assert.That(applied.ProxyPassword, Is.Empty);
                Assert.That(File.ReadAllText(path), Does.Not.Contain("must-not-persist"));
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task UpdateCheckerShouldSelectCurrentPlatformArchiveAndChecksum()
    {
        string runtime = ReleaseUpdateChecker.CurrentRuntimeIdentifier();
        string suffix = OperatingSystem.IsWindows() ? ".zip" : ".tar.gz";
        string json = $$"""
            {"tag_name":"v9.0.0","html_url":"https://example/release","body":"Release notes",
             "assets":[
               {"name":"RatioForge-9.0.0-{{runtime}}{{suffix}}","browser_download_url":"https://example/package","size":123},
               {"name":"RatioForge-9.0.0-{{runtime}}.sha256","browser_download_url":"https://example/checksum","size":64}
             ]}
            """;
        using var http = new HttpClient(new StaticHandler(Encoding.UTF8.GetBytes(json)));
        var checker = new ReleaseUpdateChecker(http);

        ReleaseUpdateResult result = await checker.CheckAsync("1.2.0");

        Assert.Multiple(() =>
        {
            Assert.That(result.Package?.Name, Is.EqualTo($"RatioForge-9.0.0-{runtime}{suffix}"));
            Assert.That(result.Checksum?.Name, Is.EqualTo($"RatioForge-9.0.0-{runtime}.sha256"));
            Assert.That(result.ReleaseNotes, Is.EqualTo("Release notes"));
        });
    }

    [Test]
    public async Task UpdateDownloaderShouldRejectInvalidChecksum()
    {
        byte[] package = Encoding.UTF8.GetBytes("package data");
        string manifest = $"{new string('0', 64)}  package.zip\n";
        using var http = new HttpClient(new RoutingHandler(package, Encoding.UTF8.GetBytes(manifest)));
        var downloader = new ReleasePackageDownloader(http);
        var release = new ReleaseUpdateResult(
            new Version(1, 0), new Version(2, 0), true, "https://example/release", Package:
            new ReleaseAsset("package.zip", "https://example/package", package.Length), Checksum:
            new ReleaseAsset("package.sha256", "https://example/checksum", manifest.Length));
        string directory = Path.Combine(Path.GetTempPath(), $"ratioforge-update-{Guid.NewGuid():N}");
        try
        {
            Assert.That(
                async () => await downloader.DownloadAndVerifyAsync(release, directory),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(File.Exists(Path.Combine(directory, "package.zip")), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task UpdateDownloaderShouldPublishOnlyVerifiedPackage()
    {
        byte[] package = Encoding.UTF8.GetBytes("verified package data");
        string hash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        string manifest = $"{hash}  package.zip\n";
        using var http = new HttpClient(new RoutingHandler(package, Encoding.UTF8.GetBytes(manifest)));
        var downloader = new ReleasePackageDownloader(http);
        var release = new ReleaseUpdateResult(
            new Version(1, 0), new Version(2, 0), true, "https://example/release", Package:
            new ReleaseAsset("package.zip", "https://example/package", package.Length), Checksum:
            new ReleaseAsset("package.sha256", "https://example/checksum", manifest.Length));
        string directory = Path.Combine(Path.GetTempPath(), $"ratioforge-update-{Guid.NewGuid():N}");
        try
        {
            string path = await downloader.DownloadAndVerifyAsync(release, directory);

            Assert.Multiple(() =>
            {
                Assert.That(path, Is.EqualTo(Path.Combine(directory, "package.zip")));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(package));
                Assert.That(File.Exists(path + ".download"), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);

    private sealed class StaticHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    private sealed class RoutingHandler(byte[] package, byte[] checksum) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] content = request.RequestUri?.AbsolutePath == "/checksum" ? checksum : package;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }

    private sealed class CapturingHandler(string response) : HttpMessageHandler
    {
        public string RequestUri { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.ASCII.GetBytes(response)),
            });
        }
    }
}
