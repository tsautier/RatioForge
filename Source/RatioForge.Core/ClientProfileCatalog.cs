namespace RatioForge;

/// <summary>Provides the torrent client identities supported by RatioForge.</summary>
public static class ClientProfileCatalog
{
    /// <summary>The latest stable qBittorrent identity verified by the project.</summary>
    public const string DefaultProfileName = "qBittorrent 5.2.3";

    private static readonly string[] Names =
    [
        "qBittorrent 5.2.3", "qBittorrent 5.1.3", "qBittorrent 5.1.2", "qBittorrent 4.6.3",
        "qBittorrent 4.5.5", "qBittorrent 4.4.5", "qBittorrent 4.2.3", "Transmission 4.1.3",
        "Transmission 2.92 (14714)", "Transmission 2.82 (14160)", "Deluge 2.2.0", "Deluge 1.3.15",
        "Deluge 1.2.0", "Deluge 0.5.8.7", "Deluge 0.5.8.6", "BiglyBT 4.1.0.0", "KTorrent 26.04.3",
        "KTorrent 2.2.1", "uTorrent 3.6.0 (build 46590)", "uTorrent 3.3.2", "uTorrent 3.3.0",
        "uTorrent 3.2.0", "uTorrent 2.0.1 (build 19078)", "uTorrent 1.8.5 (build 17414)",
        "uTorrent 1.8.1-beta(11903)", "uTorrent 1.8.0", "uTorrent 1.7.7", "uTorrent 1.7.6",
        "uTorrent 1.7.5", "uTorrent 1.6.1", "uTorrent 1.6", "BitComet 1.20", "BitComet 1.03",
        "BitComet 0.98", "BitComet 0.96", "BitComet 0.93", "BitComet 0.92", "Vuze 4.2.0.8",
        "Azureus 3.1.1.0", "Azureus 3.0.5.0", "Azureus 3.0.4.2", "Azureus 3.0.3.4",
        "Azureus 3.0.2.2", "Azureus 2.5.0.4", "BitTorrent 6.0.3 (8642)", "ABC 3.1",
        "BitLord 1.1", "BTuga 2.1.8", "BitTornado 0.3.17", "Burst 3.1.0b", "BitTyrant 1.1",
        "BitSpirit 3.6.0.200", "BitSpirit 3.1.0.077", "Gnome BT 0.0.28-1"
    ];

    /// <summary>Gets all supported identities, with current clients first.</summary>
    public static IReadOnlyList<ClientProfile> All { get; } = Names.Select(CreateProfile).ToArray();

    /// <summary>Gets the identity selected for a new tracker session.</summary>
    public static ClientProfile Default { get; } = All.Single(profile => profile.Name == DefaultProfileName);

    private static ClientProfile CreateProfile(string name)
    {
        TorrentClient client = TorrentClientFactory.GetClient(name);
        const string marker = "User-Agent: ";
        string headers = client.Headers ?? string.Empty;
        int start = headers.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string userAgent = name;
        if (start >= 0)
        {
            start += marker.Length;
            int end = headers.IndexOf("\r\n", start, StringComparison.Ordinal);
            userAgent = headers[start..(end >= 0 ? end : headers.Length)];
        }

        return new ClientProfile(name, userAgent, client.DefNumWant);
    }
}
