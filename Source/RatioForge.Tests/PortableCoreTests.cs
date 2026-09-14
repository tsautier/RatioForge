namespace RatioForge.Tests;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class PortableCoreTests
{
    [Test]
    public void TorrentDocumentShouldExposePortableMetadata()
    {
        TorrentDocument document = TorrentDocument.Load(FixturePath("single-file.torrent"));

        Assert.Multiple(() =>
        {
            Assert.That(document.Name, Is.EqualTo("sample.bin"));
            Assert.That(document.Tracker, Is.EqualTo("https://tracker.example/announce"));
            Assert.That(document.TotalSize, Is.EqualTo(12345));
            Assert.That(document.InfoHash, Has.Length.EqualTo(40));
            Assert.That(document.FileCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClientProfileCatalogShouldExposeEveryLegacyIdentity()
    {
        Assert.That(ClientProfileCatalog.All, Has.Count.EqualTo(54));
        Assert.That(ClientProfileCatalog.All.Select(profile => profile.Name), Does.Contain("qBittorrent 5.2.3"));
        Assert.That(ClientProfileCatalog.All.All(profile => !string.IsNullOrWhiteSpace(profile.UserAgent)), Is.True);
    }

    [Test]
    public void DefaultClientProfileShouldBeLatestStableQBittorrent()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ClientProfileCatalog.Default.Name, Is.EqualTo("qBittorrent 5.2.3"));
            Assert.That(ClientProfileCatalog.Default.UserAgent, Is.EqualTo("qBittorrent/5.2.3"));
            Assert.That(ClientProfileCatalog.All[0], Is.SameAs(ClientProfileCatalog.Default));
        });
    }

    [Test]
    public void ApplicationSettingsShouldRoundTripWithoutPersistingProxyPassword()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-settings-{Guid.NewGuid():N}.json");
        try
        {
            var expected = new ApplicationSettings
            {
                DefaultProfileName = "Transmission 4.1.3",
                ThemeMode = ApplicationThemeMode.Light,
                LocalAddress = "2001:db8::42",
                Port = 51413,
                RandomizeUpload = true,
                EnableDebugLog = true,
                ProxyMode = TrackerProxyMode.Socks5,
                ProxyHost = "proxy.example",
                ProxyPort = 1080,
                ProxyUsername = "user",
                ProxyPassword = "not-persisted",
            };

            ApplicationSettingsStore.Save(expected, path);
            ApplicationSettings actual = ApplicationSettingsStore.Load(path);
            string json = File.ReadAllText(path);

            Assert.Multiple(() =>
            {
                Assert.That(actual.DefaultProfileName, Is.EqualTo("Transmission 4.1.3"));
                Assert.That(actual.ThemeMode, Is.EqualTo(ApplicationThemeMode.Light));
                Assert.That(actual.LocalAddress, Is.EqualTo("2001:db8::42"));
                Assert.That(actual.Port, Is.EqualTo(51413));
                Assert.That(actual.RandomizeUpload, Is.True);
                Assert.That(actual.EnableDebugLog, Is.True);
                Assert.That(actual.ProxyMode, Is.EqualTo(TrackerProxyMode.Socks5));
                Assert.That(actual.ProxyHost, Is.EqualTo("proxy.example"));
                Assert.That(actual.ProxyPassword, Is.Empty);
                Assert.That(json, Does.Not.Contain("not-persisted"));
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
    public async Task ReleaseUpdateCheckerShouldUseLatestPublishedGitHubRelease()
    {
        var handler = new StubHandler("{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/tsautier/RatioForge/releases/tag/v1.2.0\"}");
        using var client = new HttpClient(handler);
        var checker = new ReleaseUpdateChecker(client);

        ReleaseUpdateResult result = await checker.CheckAsync("1.1.1");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsUpdateAvailable, Is.True);
            Assert.That(result.LatestVersion, Is.EqualTo(new Version(1, 2, 0)));
            Assert.That(result.ReleaseUrl, Is.EqualTo("https://github.com/tsautier/RatioForge/releases/tag/v1.2.0"));
            Assert.That(handler.RequestUri, Is.EqualTo(ReleaseUpdateChecker.LatestReleaseApiUrl));
            Assert.That(handler.UserAgent, Is.EqualTo("RatioForge/1.1.1"));
        });
    }

    [Test]
    public async Task ReleaseUpdateCheckerShouldAcceptTagWithoutPrefix()
    {
        var handler = new StubHandler("{\"tag_name\":\"1.1.1\"}");
        using var client = new HttpClient(handler);
        var checker = new ReleaseUpdateChecker(client);

        ReleaseUpdateResult result = await checker.CheckAsync("1.1.1");

        Assert.That(result.IsUpdateAvailable, Is.False);
        Assert.That(result.ReleaseUrl, Is.EqualTo(ReleaseUpdateChecker.LatestReleasePageUrl));
    }

    [Test]
    public void ClientProfileShouldCreateUsableSessionIdentity()
    {
        ClientIdentity identity = ClientProfileCatalog.Default.CreateIdentity();

        Assert.Multiple(() =>
        {
            Assert.That(identity.Key, Is.Not.Empty);
            Assert.That(identity.PeerId, Is.Not.Empty);
        });
    }

    [Test]
    public void RateRandomizerShouldOnlyReturnWholeValuesInsideBounds()
    {
        decimal[] values = Enumerable.Range(0, 100)
            .Select(_ => RateRandomizer.NextInteger(21.25m, 24.75m))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(values, Is.All.GreaterThanOrEqualTo(22m));
            Assert.That(values, Is.All.LessThanOrEqualTo(24m));
            Assert.That(values.All(value => decimal.Truncate(value) == value), Is.True);
        });
    }

    [Test]
    public void ApplicationSettingsShouldNormalizeRandomRateBoundsToWholeSupportedValues()
    {
        var settings = new ApplicationSettings
        {
            MinimumUploadRateKib = 21.6m,
            MaximumUploadRateKib = 2_000_000m,
            MinimumDownloadRateKib = -10m,
            MaximumDownloadRateKib = 20.4m,
        };

        settings.Normalize();

        Assert.Multiple(() =>
        {
            Assert.That(settings.MinimumUploadRateKib, Is.EqualTo(22m));
            Assert.That(settings.MaximumUploadRateKib, Is.EqualTo(1_048_576m));
            Assert.That(settings.MinimumDownloadRateKib, Is.Zero);
            Assert.That(settings.MaximumDownloadRateKib, Is.EqualTo(20m));
        });
    }

    [Test]
    public void DebugLogShouldAppendTimestampedEntry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-debug-{Guid.NewGuid():N}.log");
        try
        {
            DebugLogStore.Append("diagnostic entry", path);

            string contents = File.ReadAllText(path);
            Assert.Multiple(() =>
            {
                Assert.That(contents, Does.Contain("[DEBUG] diagnostic entry"));
                Assert.That(contents, Does.Match(@"^\d{4}-\d{2}-\d{2}T"));
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
    public void SensitiveDataRedactorShouldRemoveTrackerCredentials()
    {
        const string passkey = "0123456789abcdef0123456789abcdef";
        string message = $"tracker=https://user:password@tracker.example/{passkey}/announce?passkey={passkey}&mode=compact token:another-secret key=ABC123 peer_id=-qB5230-session info_hash=deadbeef";

        string redacted = SensitiveDataRedactor.Redact(message);

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain(passkey));
            Assert.That(redacted, Does.Not.Contain("another-secret"));
            Assert.That(redacted, Does.Not.Contain("user:password"));
            Assert.That(redacted, Does.Not.Contain("ABC123"));
            Assert.That(redacted, Does.Not.Contain("-qB5230-session"));
            Assert.That(redacted, Does.Not.Contain("deadbeef"));
            Assert.That(redacted, Does.Contain("REDACTED"));
            Assert.That(redacted, Does.Contain("mode=compact"));
        });
    }

    [Test]
    public void DebugLogShouldNeverPersistTrackerSecrets()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-redaction-{Guid.NewGuid():N}.log");
        const string secret = "abcdef0123456789abcdef0123456789";
        try
        {
            DebugLogStore.Append($"announce=https://tracker.example/{secret}/announce?token={secret}", path);

            string contents = File.ReadAllText(path);
            Assert.That(contents, Does.Not.Contain(secret));
            Assert.That(contents, Does.Contain("REDACTED"));
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
    public void DebugLogShouldCreateAnEmptyFileOnDemand()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-empty-{Guid.NewGuid():N}.log");
        try
        {
            Assert.That(DebugLogStore.EnsureFile(path), Is.EqualTo(path));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.Zero);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestCase(TrackerProxyMode.None)]
    [TestCase(TrackerProxyMode.System)]
    [TestCase(TrackerProxyMode.Http)]
    [TestCase(TrackerProxyMode.Socks5)]
    public void TrackerProxyModesShouldCreateHttpTransport(TrackerProxyMode mode)
    {
        var proxy = new TrackerProxyOptions(mode, "127.0.0.1", 8080, "user", "password");

        using HttpClient client = TrackerAnnounceClient.CreateHttpClient(null, proxy: proxy);

        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
    }

    [Test]
    public void ExplicitProxyShouldRequireHost()
    {
        Assert.That(
            () => TrackerAnnounceClient.CreateHttpClient(
                null,
                proxy: new TrackerProxyOptions(TrackerProxyMode.Http)),
            Throws.ArgumentException);
    }

    [Test]
    public async Task AnnounceClientShouldParseSuccessfulTrackerResponse()
    {
        var handler = new StubHandler("d8:intervali900e5:peers0:e");
        using var client = new TrackerAnnounceClient(new HttpClient(handler));
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = "https://tracker.example/announce",
        };
        ClientProfile profile = ClientProfileCatalog.All.First(item => item.Name == "qBittorrent 5.2.3");

        TrackerAnnounceResult result = await client.AnnounceAsync(
            new TrackerAnnounceOptions(torrent, profile, 1024, 2048, 6881, 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IntervalSeconds, Is.EqualTo(900));
            Assert.That(handler.RequestUri, Does.StartWith("https://tracker.example/announce?"));
            Assert.That(handler.UserAgent, Is.EqualTo("qBittorrent/5.2.3"));
        });
    }

    [Test]
    public async Task AnnounceClientShouldReuseProvidedSessionIdentity()
    {
        var handler = new StubHandler("d8:intervali900e5:peers0:e");
        using var client = new TrackerAnnounceClient(new HttpClient(handler));
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = "https://tracker.example/announce",
        };
        var identity = new ClientIdentity("fixed-key", "-qB5230-fixedPeerId");

        await client.AnnounceAsync(new TrackerAnnounceOptions(
            torrent,
            ClientProfileCatalog.Default,
            0,
            0,
            6881,
            50,
            Identity: identity));

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestUri, Does.Contain("key=fixed-key"));
            Assert.That(handler.RequestUri, Does.Contain("peer_id=-qB5230-fixedPeerId"));
        });
    }

    [Test]
    public async Task ManualAnnounceShouldSendCurrentCountersWithoutLifecycleEvent()
    {
        var handler = new StubHandler("d8:intervali900e5:peers0:e");
        using var client = new TrackerAnnounceClient(new HttpClient(handler));
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = "https://tracker.example/announce",
        };

        await client.AnnounceAsync(new TrackerAnnounceOptions(
            torrent,
            ClientProfileCatalog.Default,
            131072,
            654320,
            6881,
            50,
            string.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestUri, Does.Contain("uploaded=131072"));
            Assert.That(handler.RequestUri, Does.Contain("downloaded=654320"));
            Assert.That(handler.RequestUri, Does.Not.Contain("event="));
        });
    }

    [Test]
    public async Task AnnounceClientShouldAcceptIpv6TrackerAndLocalAddress()
    {
        var handler = new StubHandler("d8:intervali900e5:peers0:e");
        using var client = new TrackerAnnounceClient(new HttpClient(handler));
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = "http://[2001:db8::10]/announce",
        };
        ClientProfile profile = ClientProfileCatalog.All.First(item => item.Name == "BitComet 1.20");

        TrackerAnnounceResult result = await client.AnnounceAsync(
            new TrackerAnnounceOptions(torrent, profile, 0, 0, 6881, 50, "stopped", "2001:db8::42"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(handler.RequestUri, Does.StartWith("http://[2001:db8::10]/announce?"));
            Assert.That(handler.RequestUri, Does.Contain("localip=2001%3Adb8%3A%3A42"));
        });
    }

    [Test]
    public async Task AnnounceClientShouldAcceptHttpsIpv6TrackerUrl()
    {
        var handler = new StubHandler("d8:intervali900e5:peers0:e");
        using var client = new TrackerAnnounceClient(new HttpClient(handler));
        TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
        {
            Tracker = "https://[2001:db8::20]/announce",
        };
        ClientProfile profile = ClientProfileCatalog.All.First(item => item.Name == "qBittorrent 5.2.3");

        TrackerAnnounceResult result = await client.AnnounceAsync(
            new TrackerAnnounceOptions(torrent, profile, 0, 0, 6881, 50, "started", "2001:db8::42"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(handler.RequestUri, Does.StartWith("https://[2001:db8::20]/announce?"));
        });
    }

    [Test]
    public void NetworkAddressCatalogShouldExposeIpv6LoopbackWhenSupported()
    {
        if (!System.Net.Sockets.Socket.OSSupportsIPv6)
        {
            Assert.Ignore("IPv6 is not supported by this operating system.");
        }

        Assert.That(NetworkAddressCatalog.GetLocalAddresses(), Does.Contain("::1"));
    }

    [Test]
    public async Task AnnounceClientShouldBindIpv6SourceAddress()
    {
        if (!Socket.OSSupportsIPv6)
        {
            Assert.Ignore("IPv6 is not supported by this operating system.");
        }

        var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        IPAddress remoteAddress = null;
        Task server = Task.Run(async () =>
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync();
            remoteAddress = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
            await using NetworkStream stream = connection.GetStream();
            var buffer = new byte[4096];
            int bytesRead = await stream.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                throw new IOException("The IPv6 test client closed before sending its request.");
            }
            byte[] body = Encoding.ASCII.GetBytes("d8:intervali900e5:peers0:e");
            byte[] response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response);
            await stream.WriteAsync(body);
        });

        try
        {
            using var client = new TrackerAnnounceClient();
            TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
            {
                Tracker = $"http://[::1]:{port}/announce",
            };
            ClientProfile profile = ClientProfileCatalog.All.First(item => item.Name == "qBittorrent 5.2.3");

            TrackerAnnounceResult result;
            try
            {
                result = await client.AnnounceAsync(
                    new TrackerAnnounceOptions(torrent, profile, 0, 0, 6881, 50, "started", "::1"));
            }
            catch (Exception clientException)
            {
                try
                {
                    await server;
                }
                catch (Exception serverException)
                {
                    throw new AggregateException(clientException, serverException);
                }

                throw;
            }
            await server;

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(remoteAddress, Is.EqualTo(IPAddress.IPv6Loopback));
            });
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task AnnounceClientShouldNegotiateHttpsOverIpv6()
    {
        if (!Socket.OSSupportsIPv6)
        {
            Assert.Ignore("IPv6 is not supported by this operating system.");
        }

        using X509Certificate2 certificate = CreateIpv6Certificate();
        var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = Task.Run(async () =>
        {
            using TcpClient connection = await listener.AcceptTcpClientAsync();
            await using var tls = new SslStream(connection.GetStream(), leaveInnerStreamOpen: false);
            await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            });
            var buffer = new byte[4096];
            int bytesRead = await tls.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                throw new IOException("The HTTPS IPv6 client closed before sending its request.");
            }

            byte[] body = Encoding.ASCII.GetBytes("d8:intervali900e5:peers0:e");
            byte[] response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await tls.WriteAsync(response);
            await tls.WriteAsync(body);
        });

        try
        {
            using HttpClient httpsClient = TrackerAnnounceClient.CreateHttpClient(
                IPAddress.IPv6Loopback,
                (_, _, _, _) => true);
            using var client = new TrackerAnnounceClient(httpsClient);
            TorrentDocument torrent = TorrentDocument.Load(FixturePath("single-file.torrent")) with
            {
                Tracker = $"https://[::1]:{port}/announce",
            };
            ClientProfile profile = ClientProfileCatalog.All.First(item => item.Name == "qBittorrent 5.2.3");

            TrackerAnnounceResult result;
            try
            {
                result = await client.AnnounceAsync(
                    new TrackerAnnounceOptions(torrent, profile, 0, 0, 6881, 50, "started", "::1"));
            }
            catch (Exception clientException)
            {
                try
                {
                    await server;
                }
                catch (Exception serverException)
                {
                    throw new AggregateException(clientException, serverException);
                }

                throw;
            }
            await server;

            Assert.That(result.IsSuccess, Is.True);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static X509Certificate2 CreateIpv6Certificate()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=::1", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") },
            critical: false));
        using X509Certificate2 generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }

    private static string FixturePath(string fileName) =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public string RequestUri { get; private set; }

        public string UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.ASCII.GetBytes(response)),
            });
        }
    }
}
