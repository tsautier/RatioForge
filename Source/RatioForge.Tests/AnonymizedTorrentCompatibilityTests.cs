namespace RatioForge.Tests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using BitTorrent;
using NUnit.Framework;

/// <summary>Exercises anonymized metainfo shapes observed in private and public torrents.</summary>
[TestFixture]
public sealed class AnonymizedTorrentCompatibilityTests
{
    [OneTimeSetUp]
    public void RegisterLegacyMetainfoEncoding() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Test]
    public void AnonymizedTieredTorrentShouldPreserveTierOrderAndRemoveFlattenedDuplicates()
    {
        const string primary = "https://cdn.tracker.example/REDACTED/announce";
        var root = SingleFileTorrent(primary, "release-video.mkv", 13_477_232_381);
        var tiers = new ValueList();
        tiers.Add(Tier(primary, "https://backup.tracker.example/announce?token=REDACTED"));
        tiers.Add(Tier("udp://[2001:db8::25]:6969/announce", primary));
        root.Add("announce-list", tiers);

        WithTorrent(root, path =>
        {
            TorrentDocument document = TorrentDocument.Load(path);
            Assert.Multiple(() =>
            {
                Assert.That(document.TotalSize, Is.EqualTo(13_477_232_381));
                Assert.That(document.TrackerTiers, Has.Count.EqualTo(2));
                Assert.That(document.Trackers, Is.EqualTo(new[]
                {
                    primary,
                    "https://backup.tracker.example/announce?token=REDACTED",
                    "udp://[2001:db8::25]:6969/announce",
                }));
            });
        });
    }

    [Test]
    public void FlatAndPartiallyMalformedAnnounceListShouldKeepUsableTrackers()
    {
        const string primary = "https://tracker.example/announce";
        var root = SingleFileTorrent(primary, "linux-image.iso", 4_294_967_296);
        var announceList = new ValueList();
        announceList.Add(new ValueString("https://secondary.example/announce"));
        announceList.Add(new ValueNumber(42));
        announceList.Add(Tier("udp://tracker.example:1337/announce"));
        root.Add("announce-list", announceList);

        WithTorrent(root, path =>
        {
            TorrentDocument document = TorrentDocument.Load(path);
            Assert.That(document.Trackers, Is.EqualTo(new[]
            {
                primary,
                "https://secondary.example/announce",
                "udp://tracker.example:1337/announce",
            }));
        });
    }

    [Test]
    public void AnonymizedMultiFileTorrentShouldRetainLargeAggregateSizeAndNestedPaths()
    {
        var root = new ValueDictionary();
        root.Add("announce", new ValueString("https://tracker.example/announce"));
        var info = new ValueDictionary();
        info.Add("name", new ValueString("release-pack"));
        info.Add("piece length", new ValueNumber(4_194_304));
        info.Add("pieces", new ValueString(new string('P', 40)));
        var files = new ValueList();
        files.Add(FileEntry(7_000_000_000, "disc-1", "video.mkv"));
        files.Add(FileEntry(128_000, "subs", "english.srt"));
        info.Add("files", files);
        root.Add("info", info);

        WithTorrent(root, path =>
        {
            var torrent = new Torrent(path);
            Assert.Multiple(() =>
            {
                Assert.That(torrent.SingleFile, Is.False);
                Assert.That(torrent.totalLength, Is.EqualTo(7_000_128_000));
                Assert.That(torrent.PhysicalFiles, Has.Count.EqualTo(2));
                Assert.That(torrent.PhysicalFiles.Select(file => file.RelativePath),
                    Is.EqualTo(new[]
                    {
                        Path.Combine("disc-1", "video.mkv"),
                        Path.Combine("subs", "english.srt"),
                    }));
            });
        });
    }

    private static ValueDictionary SingleFileTorrent(string tracker, string name, long length)
    {
        var root = new ValueDictionary();
        root.Add("announce", new ValueString(tracker));
        var info = new ValueDictionary();
        info.Add("length", new ValueNumber(length));
        info.Add("name", new ValueString(name));
        info.Add("piece length", new ValueNumber(16_384));
        info.Add("pieces", new ValueString(new string('H', 20)));
        root.Add("info", info);
        return root;
    }

    private static ValueList Tier(params string[] trackers)
    {
        var tier = new ValueList();
        foreach (string tracker in trackers)
        {
            tier.Add(new ValueString(tracker));
        }

        return tier;
    }

    private static ValueDictionary FileEntry(long length, params string[] pathParts)
    {
        var file = new ValueDictionary();
        file.Add("length", new ValueNumber(length));
        var path = new ValueList();
        foreach (string part in pathParts)
        {
            path.Add(new ValueString(part));
        }

        file.Add("path", path);
        return file;
    }

    private static void WithTorrent(ValueDictionary root, Action<string> assertion)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-anonymized-{Guid.NewGuid():N}.torrent");
        try
        {
            File.WriteAllBytes(path, root.Encode());
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
