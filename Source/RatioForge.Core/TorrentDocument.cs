namespace RatioForge;

using BitTorrent;

/// <summary>Metadata loaded from a BitTorrent metainfo file.</summary>
public sealed record TorrentDocument(
    string FilePath,
    string Name,
    string Tracker,
    string InfoHash,
    long TotalSize,
    int FileCount,
    IReadOnlyList<IReadOnlyList<string>>? TrackerTiers = null)
{
    /// <summary>All unique trackers in tier order, including the primary announce URL.</summary>
    public IReadOnlyList<string> Trackers => (TrackerTiers ?? [[Tracker]])
        .SelectMany(tier => tier)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    /// <summary>Loads and validates a local <c>.torrent</c> file.</summary>
    public static TorrentDocument Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The torrent file was not found.", fullPath);
        }

        var torrent = new Torrent(fullPath);
        IReadOnlyList<IReadOnlyList<string>> trackerTiers = ReadTrackerTiers(torrent);
        return new TorrentDocument(
            fullPath,
            torrent.Name,
            torrent.Announce,
            Convert.ToHexString(torrent.InfoHash).ToLowerInvariant(),
            checked((long)torrent.totalLength),
            torrent.PhysicalFiles.Count,
            trackerTiers);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadTrackerTiers(Torrent torrent)
    {
        var tiers = new List<IReadOnlyList<string>>();
        if (torrent.Data.Contains("announce-list") && torrent.Data["announce-list"] is ValueList announceList)
        {
            foreach (IBEncodeValue tierValue in announceList.Values)
            {
                IEnumerable<IBEncodeValue> values = tierValue is ValueList tier
                    ? tier.Values
                    : [tierValue];
                string[] trackers = values
                    .OfType<ValueString>()
                    .Select(BEncode.String)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (trackers.Length > 0)
                {
                    tiers.Add(trackers);
                }
            }
        }

        if (tiers.Count == 0)
        {
            tiers.Add([torrent.Announce]);
        }
        else if (!tiers.SelectMany(tier => tier).Contains(torrent.Announce, StringComparer.Ordinal))
        {
            tiers.Insert(0, [torrent.Announce]);
        }

        return tiers;
    }
}
