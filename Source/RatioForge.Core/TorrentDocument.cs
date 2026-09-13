namespace RatioForge;

using BitTorrent;

/// <summary>Metadata loaded from a BitTorrent metainfo file.</summary>
public sealed record TorrentDocument(
    string FilePath,
    string Name,
    string Tracker,
    string InfoHash,
    long TotalSize,
    int FileCount)
{
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
        return new TorrentDocument(
            fullPath,
            torrent.Name,
            torrent.Announce,
            Convert.ToHexString(torrent.InfoHash).ToLowerInvariant(),
            checked((long)torrent.totalLength),
            torrent.PhysicalFiles.Count);
    }
}
