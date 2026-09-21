namespace RatioForge;

using System.Text.Json;

/// <summary>Provides the torrent client identities supported by RatioForge.</summary>
public static class ClientProfileCatalog
{
    /// <summary>The latest stable qBittorrent identity verified by the project.</summary>
    public const string DefaultProfileName = "qBittorrent 5.2.3";

    private static readonly string[] LegacyNames =
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

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    static ClientProfileCatalog() => Reload();

    /// <summary>Reloads the embedded catalog and the optional application-data override.</summary>
    public static void Reload()
    {
        LoadWarning = string.Empty;
        Definitions = LoadDefinitions();
        var profiles = Definitions.Select(definition => new ClientProfile(
            definition.Name,
            definition.UserAgent,
            definition.DefaultPeerCount,
            definition)).ToList();
        foreach (string name in LegacyNames.Where(name => profiles.All(profile => profile.Name != name)))
        {
            profiles.Add(CreateLegacyProfile(name));
        }

        All = profiles;
        Default = All.Single(profile => profile.Name == DefaultProfileName);
    }

    /// <summary>Path of the optional user catalog that adds or replaces profiles at startup.</summary>
    public static string UserCatalogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RatioForge",
        "clients.json");

    /// <summary>Gets a warning when a custom catalog was ignored.</summary>
    public static string LoadWarning { get; private set; } = string.Empty;

    /// <summary>Gets all supported identities, with current data-driven clients first.</summary>
    public static IReadOnlyList<ClientProfile> All { get; private set; } = [];

    /// <summary>Gets the identity selected for a new tracker session.</summary>
    public static ClientProfile Default { get; private set; } = null!;

    /// <summary>Gets the effective data-driven definitions, including user overrides.</summary>
    public static IReadOnlyList<ClientProfileDefinition> Definitions { get; private set; } = [];

    /// <summary>Exports the effective data-driven client catalog.</summary>
    public static void Export(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        WriteCatalog(path, Definitions);
    }

    /// <summary>Validates and installs a custom client catalog, then reloads the effective profiles.</summary>
    public static int Import(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ClientCatalogDocument document = Deserialize(File.ReadAllText(sourcePath));
        IReadOnlyList<ClientProfileDefinition> imported = ValidateAndMerge(document.Clients, []);
        WriteCatalog(UserCatalogPath, imported);
        Reload();
        return imported.Count;
    }

    private static ClientProfile CreateLegacyProfile(string name)
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

    private static IReadOnlyList<ClientProfileDefinition> LoadDefinitions()
    {
        using Stream stream = typeof(ClientProfileCatalog).Assembly.GetManifestResourceStream("RatioForge.clients.json")
            ?? throw new InvalidOperationException("The embedded client catalog is missing.");
        using var reader = new StreamReader(stream);
        ClientCatalogDocument builtIn = Deserialize(reader.ReadToEnd());
        if (!File.Exists(UserCatalogPath))
        {
            return ValidateAndMerge(builtIn.Clients, []);
        }

        try
        {
            ClientCatalogDocument custom = Deserialize(File.ReadAllText(UserCatalogPath));
            return ValidateAndMerge(builtIn.Clients, custom.Clients);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LoadWarning = $"Custom clients.json ignored: {exception.Message}";
            return ValidateAndMerge(builtIn.Clients, []);
        }
    }

    private static ClientCatalogDocument Deserialize(string json)
    {
        ClientCatalogDocument document = JsonSerializer.Deserialize<ClientCatalogDocument>(json, SerializerOptions)
            ?? throw new InvalidDataException("The client catalog is empty.");
        if (document.FormatVersion != 1)
        {
            throw new InvalidDataException($"Unsupported client profile format version {document.FormatVersion}.");
        }

        return document;
    }

    private static IReadOnlyList<ClientProfileDefinition> ValidateAndMerge(
        IEnumerable<ClientProfileDefinition> builtIn,
        IEnumerable<ClientProfileDefinition> custom)
    {
        var merged = builtIn.ToList();
        foreach (ClientProfileDefinition definition in custom)
        {
            if (definition is null)
            {
                throw new InvalidDataException("The client catalog contains a null entry.");
            }

            definition.Validate();
            int index = merged.FindIndex(item => item.Name.Equals(definition.Name, StringComparison.Ordinal));
            if (index >= 0)
            {
                merged[index] = definition;
            }
            else
            {
                merged.Add(definition);
            }
        }

        foreach (ClientProfileDefinition definition in merged)
        {
            definition.Validate();
        }

        if (merged.GroupBy(item => item.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new InvalidDataException("The client catalog contains duplicate names.");
        }

        return merged;
    }

    internal static IReadOnlyList<ClientProfileDefinition> ParseDefinitions(string json) =>
        ValidateAndMerge(Deserialize(json).Clients, []);

    private static void WriteCatalog(string path, IEnumerable<ClientProfileDefinition> definitions)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new ClientCatalogDocument { FormatVersion = 1, Clients = definitions.ToList() };
        File.WriteAllText(fullPath, JsonSerializer.Serialize(document, SerializerOptions));
    }

    private sealed class ClientCatalogDocument
    {
        public int FormatVersion { get; init; } = 1;

        public List<ClientProfileDefinition> Clients { get; init; } = [];
    }
}
