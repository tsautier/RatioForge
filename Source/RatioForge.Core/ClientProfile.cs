namespace RatioForge;

/// <summary>A public, immutable view of a legacy torrent client identity.</summary>
public sealed record ClientProfile(string Name, string UserAgent, int DefaultPeerCount)
{
    internal TorrentClient CreateClient() => TorrentClientFactory.GetClient(Name);

    /// <summary>Creates the client key and peer ID used for one tracker session.</summary>
    public ClientIdentity CreateIdentity()
    {
        TorrentClient client = CreateClient();
        return new ClientIdentity(client.Key, client.PeerID);
    }

    public override string ToString() => Name;
}

/// <summary>The stable identifiers sent by an emulated client during one tracker session.</summary>
public sealed record ClientIdentity(string Key, string PeerId);
