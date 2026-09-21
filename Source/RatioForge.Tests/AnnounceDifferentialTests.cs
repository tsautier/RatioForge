namespace RatioForge.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using BitTorrent;
using NUnit.Framework;

/// <summary>
/// Locks the refactored announce adapter to the parameters produced by the historical engine.
/// Fixed identities keep failures focused on parameter mapping rather than random fingerprints.
/// </summary>
[TestFixture]
public sealed class AnnounceDifferentialTests
{
    private static readonly string[] ProfileNames =
    [
        "qBittorrent 5.2.3",
        "uTorrent 3.6.0 (build 46828)",
        "BitTorrent 7.10.3 (44429)",
        "Transmission 3.00",
        "Vuze 5.7.5.0",
    ];

    private static readonly string[] Events = ["started", "", "completed", "stopped"];

    [TestCaseSource(nameof(Cases))]
    public void RefactoredAnnounceShouldMatchHistoricalParameterOutput(string profileName, string eventName)
    {
        ClientProfile profile = ClientProfileCatalog.All.Single(item => item.Name == profileName);
        var identity = new ClientIdentity("A1B2C3D4", "-qB5230-123456789012");
        var torrent = new TorrentDocument(
            "fixture.torrent", "anonymous.bin",
            "https://tracker.example/REDACTED/announce?source=legacy",
            "00112233445566778899aabbccddeeff10203040", 20_000_000, 1);
        var options = new TrackerAnnounceOptions(
            torrent, profile, 1_234_567, 7_654_329, 51413, 80, eventName,
            "2001:db8::10", Identity: identity, Left: 12_345_678);

        string refactored = TrackerAnnounceClient.BuildHttpRequestUrl(options);
        string historical = BuildHistoricalRequest(options, identity);

        Assert.Multiple(() =>
        {
            Assert.That(refactored, Is.EqualTo(historical), $"{profileName} event '{eventName}'");
            Assert.That(refactored, Does.Contain("uploaded=1228800"));
            Assert.That(refactored, Does.Contain("downloaded=7654320"));
            Assert.That(refactored, Does.Contain("left=12345678"));
            Assert.That(refactored, Does.Contain("port=51413"));
            Assert.That(refactored, eventName == "stopped" ? Does.Contain("numwant=0") : Does.Contain("numwant=80"));
        });
    }

    private static IEnumerable<TestCaseData> Cases() =>
        from profile in ProfileNames
        from eventName in Events
        select new TestCaseData(profile, eventName).SetName(
            $"AnnounceDiff_{profile.Replace(' ', '_').Replace('.', '_')}_{(eventName.Length == 0 ? "update" : eventName)}");

    private static string BuildHistoricalRequest(TrackerAnnounceOptions options, ClientIdentity identity)
    {
        var info = new TorrentInfo(options.Uploaded, options.Downloaded)
        {
            tracker = options.Torrent.Tracker,
            hash = options.Torrent.InfoHash,
            left = options.Left!.Value,
            totalsize = options.Torrent.TotalSize,
            peerID = identity.PeerId,
            port = options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            key = identity.Key,
            numberOfPeers = options.PeerCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        string trackerEvent = string.IsNullOrWhiteSpace(options.Event)
            ? string.Empty
            : "&event=" + options.Event.Trim().ToLowerInvariant();
        return TrackerUrlBuilder.BuildAnnounce(info, options.Profile.CreateClient(), trackerEvent, options.LocalIp);
    }
}
