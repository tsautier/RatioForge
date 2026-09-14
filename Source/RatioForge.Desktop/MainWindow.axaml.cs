namespace RatioForge.Desktop;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

public partial class MainWindow : Window
{
    private const string RepositoryUrl = "https://github.com/tsautier/RatioForge";
    private const string NewIssueUrl = RepositoryUrl + "/issues/new/choose";
    private const string LatestReleaseUrl = RepositoryUrl + "/releases/latest";
    private readonly ObservableCollection<string> activity = [];
    private readonly DispatcherTimer timer;
    private readonly TrackerAnnounceClient announceClient = new();
    private readonly ReleaseUpdateChecker updateChecker = new();
    private readonly ApplicationSettings settings;
    private readonly string currentVersion;
    private ClientIdentity? clientIdentity;
    private TorrentDocument? torrent;
    private CancellationTokenSource? sessionCancellation;
    private DateTimeOffset lastCounterUpdate;
    private DateTimeOffset nextAnnounce;
    private long uploaded;
    private long downloaded;
    private bool announcing;
    private int sessionGeneration;
    private string availableReleaseUrl = LatestReleaseUrl;

    public MainWindow()
    {
        InitializeComponent();
        settings = ApplicationSettingsStore.Load();
        ActivityList.ItemsSource = activity;
        ClientCombo.ItemsSource = ClientProfileCatalog.All;
        ApplySettings();
        PlatformText.Text = $".NET 10 / {GetPlatformName()}";
        currentVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionText.Text = "v" + currentVersion;
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, Timer_Tick);
        ResetTransferCounters();
        AddActivity("Ready. Open a torrent file to configure a session.");
        AddDebug($"Application started on {Environment.OSVersion}; log file: {DebugLogStore.DefaultPath}");
        Opened += async (_, _) => await CheckForUpdatesAsync(manual: false);
        Closed += (_, _) =>
        {
            sessionCancellation?.Cancel();
            sessionCancellation?.Dispose();
            announceClient.Dispose();
        };
    }

    private void Repository_Click(object? sender, RoutedEventArgs e) => OpenWebPage(RepositoryUrl);

    private void CreateIssue_Click(object? sender, RoutedEventArgs e) => OpenWebPage(NewIssueUrl);

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(manual: true);

    private void LatestRelease_Click(object? sender, RoutedEventArgs e) => OpenWebPage(availableReleaseUrl);

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(settings);
        bool saved = await window.ShowDialog<bool>(this);
        if (!saved)
        {
            return;
        }

        try
        {
            ApplicationSettingsStore.Save(settings);
            ApplySettings();
            AddActivity("Settings saved.");
            AddDebug("Settings saved.");
            StatusText.Text = "Settings saved";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not save settings";
            AddActivity("ERROR " + exception.Message, force: true);
        }
    }

    private void ApplySettings()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = settings.ThemeMode switch
            {
                ApplicationThemeMode.Dark => ThemeVariant.Dark,
                ApplicationThemeMode.Light => ThemeVariant.Light,
                _ => ThemeVariant.Default,
            };
        }

        ClientCombo.SelectedItem = ClientProfileCatalog.All.FirstOrDefault(
            profile => profile.Name == settings.DefaultProfileName) ?? ClientProfileCatalog.Default;
        var addresses = new List<string> { "Automatic (IPv4 / IPv6)" };
        addresses.AddRange(NetworkAddressCatalog.GetLocalAddresses());
        if (!string.IsNullOrWhiteSpace(settings.LocalAddress) && !addresses.Contains(settings.LocalAddress, StringComparer.Ordinal))
        {
            addresses.Add(settings.LocalAddress);
        }

        AddressCombo.ItemsSource = addresses;
        AddressCombo.SelectedItem = string.IsNullOrWhiteSpace(settings.LocalAddress)
            ? addresses[0]
            : settings.LocalAddress;
        LocalIpv4Box.Text = FormatLocalAddresses(addresses, AddressFamily.InterNetwork);
        LocalIpv6Box.Text = FormatLocalAddresses(addresses, AddressFamily.InterNetworkV6);
        PortBox.Value = settings.Port;
        PeerCountBox.Value = settings.PeerCount;
        UploadRateBox.Value = settings.UploadRateKib;
        DownloadRateBox.Value = settings.DownloadRateKib;
        IntervalBox.Value = settings.IntervalSeconds;
    }

    private async void OpenTorrent_Click(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open torrent",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("BitTorrent metainfo") { Patterns = ["*.torrent"] }],
        });
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            TorrentDocument loadedTorrent = TorrentDocument.Load(files[0].Path.LocalPath);
            StopActiveSession();
            torrent = loadedTorrent;
            ResetTransferCounters();
            TorrentPathBox.Text = torrent.FilePath;
            TorrentNameText.Text = torrent.Name;
            TorrentSizeText.Text = FormatBytes(torrent.TotalSize);
            TrackerText.Text = torrent.Tracker;
            InfoHashBox.Text = torrent.InfoHash;
            AddActivity($"Loaded {torrent.Name} ({torrent.FileCount} file(s), {FormatBytes(torrent.TotalSize)}).");
            AddDebug($"Torrent loaded: name={torrent.Name}; info_hash={torrent.InfoHash}; tracker={torrent.Tracker}");
            StatusText.Text = "Torrent loaded";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not load torrent";
            AddActivity("ERROR " + exception.Message);
        }
    }

    private void ClientCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ClientCombo.SelectedItem is ClientProfile profile)
        {
            UserAgentText.Text = profile.UserAgent;
            PeerCountBox.Value = profile.DefaultPeerCount;
            clientIdentity = profile.CreateIdentity();
            ClientKeyBox.Text = clientIdentity.Key;
            PeerIdBox.Text = clientIdentity.PeerId;
            AddDebug($"Client identity generated: client={profile.Name}; key={clientIdentity.Key}; peer_id={clientIdentity.PeerId}");
        }
    }

    private async void Start_Click(object? sender, RoutedEventArgs e)
    {
        if (torrent is null || ClientCombo.SelectedItem is not ClientProfile || clientIdentity is null)
        {
            StatusText.Text = "Open a torrent first";
            AddActivity("A torrent file and client identity are required.");
            return;
        }

        sessionCancellation = new CancellationTokenSource();
        if (settings.RandomizeUpload)
        {
            UploadRateBox.Value = RandomRate(settings.MinimumUploadRateKib, settings.MaximumUploadRateKib);
        }
        if (settings.RandomizeDownload)
        {
            DownloadRateBox.Value = RandomRate(settings.MinimumDownloadRateKib, settings.MaximumDownloadRateKib);
        }

        downloaded = (long)(torrent.TotalSize * ((double)(CompletedBox.Value ?? 0) / 100d));
        DownloadedText.Text = FormatBytes(downloaded);
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        lastCounterUpdate = DateTimeOffset.UtcNow;
        timer.Start();
        AddActivity("Session started.");
        AddDebug($"Session started: info_hash={torrent.InfoHash}; key={clientIdentity.Key}; peer_id={clientIdentity.PeerId}");
        await SendAnnounceAsync("started", sessionCancellation.Token);
    }

    private async void Stop_Click(object? sender, RoutedEventArgs e)
    {
        timer.Stop();
        CancellationToken token = sessionCancellation?.Token ?? CancellationToken.None;
        if (!token.IsCancellationRequested)
        {
            await SendAnnounceAsync("stopped", token);
        }

        StopActiveSession();
        StatusText.Text = "Stopped";
        AddActivity("Session stopped.");
    }

    private void StopActiveSession()
    {
        sessionGeneration++;
        announcing = false;
        timer.Stop();
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
        sessionCancellation = null;
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        CountdownText.Text = "-";
        nextAnnounce = default;
    }

    private void ResetTransferCounters()
    {
        uploaded = 0;
        downloaded = 0;
        CompletedBox.Value = 0;
        UploadedText.Text = FormatBytes(0);
        DownloadedText.Text = FormatBytes(0);
        CountdownText.Text = "-";
        nextAnnounce = default;
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double seconds = (now - lastCounterUpdate).TotalSeconds;
        lastCounterUpdate = now;
        uploaded += (long)((double)(UploadRateBox.Value ?? 0) * 1024d * seconds);
        downloaded += (long)((double)(DownloadRateBox.Value ?? 0) * 1024d * seconds);
        if (torrent is not null)
        {
            downloaded = Math.Min(downloaded, torrent.TotalSize);
        }

        UploadedText.Text = FormatBytes(uploaded);
        DownloadedText.Text = FormatBytes(downloaded);
        TimeSpan remaining = nextAnnounce - now;
        CountdownText.Text = remaining > TimeSpan.Zero ? remaining.ToString(@"mm\:ss") : "now";
        if (remaining <= TimeSpan.Zero && !announcing && sessionCancellation is not null)
        {
            await SendAnnounceAsync(string.Empty, sessionCancellation.Token);
        }
    }

    private async Task SendAnnounceAsync(string eventName, CancellationToken cancellationToken)
    {
        if (torrent is null || ClientCombo.SelectedItem is not ClientProfile profile || clientIdentity is null || announcing)
        {
            return;
        }

        int announceGeneration = sessionGeneration;
        announcing = true;
        StatusText.Text = "Contacting tracker...";
        try
        {
            var options = new TrackerAnnounceOptions(
                torrent,
                profile,
                uploaded,
                downloaded,
                Decimal.ToInt32(PortBox.Value ?? 6881),
                Decimal.ToInt32(PeerCountBox.Value ?? 200),
                eventName,
                AddressCombo.SelectedIndex > 0 ? AddressCombo.SelectedItem?.ToString() ?? string.Empty : string.Empty,
                settings.ToProxyOptions(),
                clientIdentity);
            AddDebug($"Announce sending: event={eventName}; tracker={torrent.Tracker}; local_ip={options.LocalIp}; uploaded={uploaded}; downloaded={downloaded}");
            TrackerAnnounceResult result = await announceClient.AnnounceAsync(options, cancellationToken);
            if (announceGeneration != sessionGeneration)
            {
                return;
            }

            int interval = result.IntervalSeconds ?? Decimal.ToInt32(IntervalBox.Value ?? 1800);
            interval = Math.Max(30, interval);
            nextAnnounce = DateTimeOffset.UtcNow.AddSeconds(interval);
            StatusText.Text = result.IsSuccess ? "Tracker accepted announce" : "Tracker rejected announce";
            AddActivity($"{(result.IsSuccess ? "OK" : "ERROR")} {result.Message} Next announce in {interval}s.");
            AddDebug($"Announce response: success={result.IsSuccess}; interval={interval}; message={result.Message}");
        }
        catch (OperationCanceledException)
        {
            if (announceGeneration == sessionGeneration)
            {
                StatusText.Text = "Cancelled";
            }
        }
        catch (Exception exception)
        {
            if (announceGeneration != sessionGeneration)
            {
                return;
            }

            int retry = Math.Max(30, Decimal.ToInt32(IntervalBox.Value ?? 1800));
            nextAnnounce = DateTimeOffset.UtcNow.AddSeconds(retry);
            StatusText.Text = "Tracker request failed";
            AddActivity("ERROR " + exception.Message);
            AddDebug("Announce failed: " + exception);
        }
        finally
        {
            if (announceGeneration == sessionGeneration)
            {
                announcing = false;
            }
        }
    }

    private void AddActivity(string message, bool force = false)
    {
        if (!settings.EnableActivityLog && !force)
        {
            return;
        }

        activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (activity.Count > 200)
        {
            activity.RemoveAt(activity.Count - 1);
        }
    }

    private void AddDebug(string message)
    {
        if (!settings.EnableDebugLog)
        {
            return;
        }

        activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  DEBUG {message}");
        while (activity.Count > 200)
        {
            activity.RemoveAt(activity.Count - 1);
        }

        try
        {
            DebugLogStore.Append(message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Could not write debug log";
        }
    }

    private void OpenWebPage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AddDebug("Opened web page: " + url);
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not open browser";
            AddActivity("ERROR " + exception.Message, force: true);
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (manual)
        {
            StatusText.Text = "Checking for updates...";
        }

        try
        {
            ReleaseUpdateResult result = await updateChecker.CheckAsync(currentVersion);
            availableReleaseUrl = result.ReleaseUrl;
            if (result.IsUpdateAvailable)
            {
                UpdateButton.Content = $"v{result.LatestVersion.ToString(3)} available";
                UpdateButton.IsVisible = true;
                StatusText.Text = "Update available";
                AddActivity($"Update v{result.LatestVersion.ToString(3)} is available.");
            }
            else if (manual)
            {
                StatusText.Text = "RatioForge is up to date";
                AddActivity($"Version {currentVersion} is up to date.");
            }

            AddDebug($"Update check: current={result.CurrentVersion}; latest={result.LatestVersion}; available={result.IsUpdateAvailable}");
        }
        catch (Exception exception)
        {
            AddDebug("Update check failed: " + exception.Message);
            if (manual)
            {
                StatusText.Text = "Could not check for updates";
                AddActivity("ERROR Could not check GitHub for the latest release.", force: true);
            }
        }
    }

    private static string FormatLocalAddresses(IEnumerable<string> addresses, AddressFamily family)
    {
        string value = string.Join(", ", addresses.Where(address =>
            IPAddress.TryParse(address, out IPAddress? parsed) && parsed.AddressFamily == family));
        return string.IsNullOrEmpty(value) ? "Not available" : value;
    }

    private static decimal RandomRate(decimal minimum, decimal maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        return minimum + ((maximum - minimum) * (decimal)Random.Shared.NextDouble());
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string GetPlatformName() =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        OperatingSystem.IsLinux() ? "Linux" : "Desktop";
}
