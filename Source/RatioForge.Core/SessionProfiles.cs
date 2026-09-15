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
    decimal MaximumDownloadRateKib)
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
            settings.RandomizeDownload, settings.MinimumDownloadRateKib, settings.MaximumDownloadRateKib);
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
        settings.SelectedSessionProfileName = Name;
        settings.Normalize();
    }

    public override string ToString() => Name;
}

/// <summary>Persists named profiles without storing proxy passwords.</summary>
public static class SessionProfileStore
{
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

        SessionProfile[] normalized = profiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .GroupBy(profile => profile.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last() with { Name = group.Key })
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllText(profilePath, JsonSerializer.Serialize(normalized, SerializerOptions));
    }
}
