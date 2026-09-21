namespace RatioForge;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>A named, reusable set of tracker session defaults.</summary>
public sealed record SessionProfile(
    string Name,
    string ClientProfileName,
    decimal UploadRateKib,
    decimal DownloadRateKib,
    int Port,
    int PeerCount,
    int IntervalSeconds,
    string LocalAddress,
    TrackerProxyMode ProxyMode,
    string ProxyHost,
    int ProxyPort,
    string ProxyUsername,
    bool RandomizeUpload,
    decimal MinimumUploadRateKib,
    decimal MaximumUploadRateKib,
    bool RandomizeDownload,
    decimal MinimumDownloadRateKib,
    decimal MaximumDownloadRateKib,
    bool PauseUploadWhenNoLeechers = true)
{
    public static SessionProfile FromSettings(string name, ApplicationSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(settings);
        return new(
            name.Trim(), settings.DefaultProfileName, settings.UploadRateKib, settings.DownloadRateKib,
            settings.Port, settings.PeerCount, settings.IntervalSeconds, settings.LocalAddress,
            settings.ProxyMode, settings.ProxyHost, settings.ProxyPort, settings.ProxyUsername,
            settings.RandomizeUpload, settings.MinimumUploadRateKib, settings.MaximumUploadRateKib,
            settings.RandomizeDownload, settings.MinimumDownloadRateKib, settings.MaximumDownloadRateKib,
            settings.PauseUploadWhenNoLeechers);
    }

    public void ApplyTo(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.DefaultProfileName = ClientProfileName;
        settings.UploadRateKib = UploadRateKib;
        settings.DownloadRateKib = DownloadRateKib;
        settings.Port = Port;
        settings.PeerCount = PeerCount;
        settings.IntervalSeconds = IntervalSeconds;
        settings.LocalAddress = LocalAddress;
        settings.ProxyMode = ProxyMode;
        settings.ProxyHost = ProxyHost;
        settings.ProxyPort = ProxyPort;
        settings.ProxyUsername = ProxyUsername;
        settings.RandomizeUpload = RandomizeUpload;
        settings.MinimumUploadRateKib = MinimumUploadRateKib;
        settings.MaximumUploadRateKib = MaximumUploadRateKib;
        settings.RandomizeDownload = RandomizeDownload;
        settings.MinimumDownloadRateKib = MinimumDownloadRateKib;
        settings.MaximumDownloadRateKib = MaximumDownloadRateKib;
        settings.PauseUploadWhenNoLeechers = PauseUploadWhenNoLeechers;
        settings.SelectedSessionProfileName = Name;
        settings.Normalize();
    }

    public override string ToString() => Name;
}

/// <summary>Persists named profiles without storing proxy passwords.</summary>
public static class SessionProfileStore
{
    private const int ExchangeFormatVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RatioForge",
        "profiles.json");

    public static IReadOnlyList<SessionProfile> Load(string? path = null)
    {
        string profilePath = path ?? DefaultPath;
        if (!File.Exists(profilePath))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<SessionProfile>>(
                File.ReadAllText(profilePath), SerializerOptions) ?? [])
                .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
                .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static void Save(IEnumerable<SessionProfile> profiles, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        string profilePath = path ?? DefaultPath;
        string? directory = Path.GetDirectoryName(profilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SessionProfile[] normalized = Normalize(profiles);
        File.WriteAllText(profilePath, JsonSerializer.Serialize(normalized, SerializerOptions));
    }

    /// <summary>Exports portable named profiles using a versioned JSON envelope.</summary>
    public static void Export(IEnumerable<SessionProfile> profiles, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SessionProfile[] normalized = Normalize(profiles);
        WriteJson(path, new SessionProfileExchangeDocument(ExchangeFormatVersion, normalized));
    }

    /// <summary>Imports and validates a versioned exchange file or a legacy profile array.</summary>
    public static IReadOnlyList<SessionProfile> Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
        SessionProfile[] profiles = json.RootElement.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.Deserialize<SessionProfile[]>(json.RootElement, SerializerOptions) ?? [],
            JsonValueKind.Object => ReadExchangeDocument(json.RootElement),
            _ => throw new InvalidDataException("The session profile file must contain a JSON object or array."),
        };
        return Normalize(profiles);
    }

    /// <summary>Merges imported profiles by name, with imported entries taking precedence.</summary>
    public static IReadOnlyList<SessionProfile> Merge(
        IEnumerable<SessionProfile> existing,
        IEnumerable<SessionProfile> imported) => Normalize(existing.Concat(imported));

    private static SessionProfile[] ReadExchangeDocument(JsonElement root)
    {
        SessionProfileExchangeDocument document = root.Deserialize<SessionProfileExchangeDocument>(SerializerOptions)
            ?? throw new InvalidDataException("The session profile exchange file is empty.");
        if (document.FormatVersion != ExchangeFormatVersion)
        {
            throw new InvalidDataException($"Unsupported session profile format version {document.FormatVersion}.");
        }

        return document.Profiles ?? [];
    }

    private static SessionProfile[] Normalize(IEnumerable<SessionProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        return profiles
            .Where(profile => profile is not null && !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => Normalize(profile))
            .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SessionProfile Normalize(SessionProfile profile)
    {
        var settings = new ApplicationSettings();
        profile.ApplyTo(settings);
        return SessionProfile.FromSettings(profile.Name.Trim(), settings);
    }

    private static void WriteJson<T>(string path, T value)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, JsonSerializer.Serialize(value, SerializerOptions));
    }

    private sealed record SessionProfileExchangeDocument(int FormatVersion, SessionProfile[] Profiles);
}
