namespace RatioForge;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Controls whether RatioForge follows the operating system theme or uses a fixed theme.</summary>
public enum ApplicationThemeMode
{
    System,
    Dark,
    Light,
}

/// <summary>Condition that ends an active tracker session automatically.</summary>
public enum SessionStopCondition
{
    Never,
    AfterDuration,
    Uploaded,
    Downloaded,
    Ratio,
}

/// <summary>Persistent, cross-platform defaults for new RatioForge sessions.</summary>
public sealed class ApplicationSettings
{
    public string SelectedSessionProfileName { get; set; } = string.Empty;

    public ApplicationThemeMode ThemeMode { get; set; } = ApplicationThemeMode.System;

    public string DefaultProfileName { get; set; } = ClientProfileCatalog.DefaultProfileName;

    public string LocalAddress { get; set; } = string.Empty;

    public int Port { get; set; } = 6881;

    public int PeerCount { get; set; } = 200;

    public decimal UploadRateKib { get; set; } = 60;

    public decimal DownloadRateKib { get; set; } = 30;

    public int IntervalSeconds { get; set; } = 1800;

    public bool RandomizeUpload { get; set; }

    public decimal MinimumUploadRateKib { get; set; } = 40;

    public decimal MaximumUploadRateKib { get; set; } = 80;

    public bool RandomizeDownload { get; set; }

    public decimal MinimumDownloadRateKib { get; set; } = 20;

    public decimal MaximumDownloadRateKib { get; set; } = 40;

    public bool EnableActivityLog { get; set; } = true;

    public bool EnableDebugLog { get; set; }

    public bool StopOnTrackerFailure { get; set; } = true;

    public SessionStopCondition StopCondition { get; set; } = SessionStopCondition.Never;

    public decimal StopValue { get; set; } = 3600;

    public TrackerProxyMode ProxyMode { get; set; } = TrackerProxyMode.None;

    public string ProxyHost { get; set; } = string.Empty;

    public int ProxyPort { get; set; } = 8080;

    public string ProxyUsername { get; set; } = string.Empty;

    [JsonIgnore]
    public string ProxyPassword { get; set; } = string.Empty;

    public TrackerProxyOptions ToProxyOptions() => new(
        ProxyMode,
        ProxyHost,
        ProxyPort,
        ProxyUsername,
        ProxyPassword);

    public void Normalize()
    {
        if (!Enum.IsDefined(ThemeMode))
        {
            ThemeMode = ApplicationThemeMode.System;
        }

        if (!Enum.IsDefined(StopCondition))
        {
            StopCondition = SessionStopCondition.Never;
        }

        if (!ClientProfileCatalog.All.Any(profile => profile.Name == DefaultProfileName))
        {
            DefaultProfileName = ClientProfileCatalog.DefaultProfileName;
        }

        if (!string.IsNullOrWhiteSpace(LocalAddress))
        {
            _ = NetworkAddressCatalog.ParseOptional(LocalAddress);
        }

        Port = Math.Clamp(Port, 1, 65535);
        PeerCount = Math.Clamp(PeerCount, 0, 500);
        UploadRateKib = Math.Clamp(UploadRateKib, 0, 1_048_576);
        DownloadRateKib = Math.Clamp(DownloadRateKib, 0, 1_048_576);
        IntervalSeconds = Math.Clamp(IntervalSeconds, 30, 86400);
        StopValue = Math.Clamp(StopValue, 1, 1_048_576);
        MinimumUploadRateKib = decimal.Round(Math.Clamp(MinimumUploadRateKib, 0, 1_048_576), 0, MidpointRounding.AwayFromZero);
        MaximumUploadRateKib = decimal.Round(Math.Clamp(MaximumUploadRateKib, MinimumUploadRateKib, 1_048_576), 0, MidpointRounding.AwayFromZero);
        MinimumDownloadRateKib = decimal.Round(Math.Clamp(MinimumDownloadRateKib, 0, 1_048_576), 0, MidpointRounding.AwayFromZero);
        MaximumDownloadRateKib = decimal.Round(Math.Clamp(MaximumDownloadRateKib, MinimumDownloadRateKib, 1_048_576), 0, MidpointRounding.AwayFromZero);
        ProxyPort = Math.Clamp(ProxyPort, 1, 65535);
    }
}

/// <summary>Writes opt-in diagnostic entries to the user's application-data directory.</summary>
public static class DebugLogStore
{
    private static readonly object SyncRoot = new();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RatioForge",
        "debug.log");

    public static void Append(string message, string? path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        string logPath = path ?? DefaultPath;
        string safeMessage = SensitiveDataRedactor.Redact(message);
        string? directory = Path.GetDirectoryName(logPath);
        lock (SyncRoot)
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                logPath,
                $"{DateTimeOffset.Now:O} [DEBUG] {safeMessage}{Environment.NewLine}");
        }
    }

    public static string EnsureFile(string? path = null)
    {
        string logPath = path ?? DefaultPath;
        string? directory = Path.GetDirectoryName(logPath);
        lock (SyncRoot)
        {
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(logPath))
            {
                File.WriteAllText(logPath, string.Empty);
            }
        }

        return logPath;
    }
}

/// <summary>Reads and writes application settings below the user's application-data directory.</summary>
public static class ApplicationSettingsStore
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
        "settings.json");

    public static ApplicationSettings Load(string? path = null)
    {
        string settingsPath = path ?? DefaultPath;
        if (!File.Exists(settingsPath))
        {
            return new ApplicationSettings();
        }

        try
        {
            ApplicationSettings settings = JsonSerializer.Deserialize<ApplicationSettings>(
                File.ReadAllText(settingsPath),
                SerializerOptions) ?? new ApplicationSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or FormatException)
        {
            return new ApplicationSettings();
        }
    }

    public static void Save(ApplicationSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        string settingsPath = path ?? DefaultPath;
        string? directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
