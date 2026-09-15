namespace RatioForge.Desktop;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

public partial class MainWindow : Window
{
    private const string RepositoryUrl = "https://github.com/tsautier/RatioForge";
    private const string NewIssueUrl = RepositoryUrl + "/issues/new/choose";
    private const string LatestReleaseUrl = RepositoryUrl + "/releases/latest";
    private readonly ObservableCollection<string> activity = [];
    private readonly ObservableCollection<AnnounceHistoryRow> announceHistory = [];
    private readonly DispatcherTimer timer;
    private readonly TrackerAnnounceClient announceClient = new();
    private readonly ReleaseUpdateChecker updateChecker = new();
    private readonly ReleasePackageDownloader updateDownloader = new();
    private readonly ApplicationSettings settings;
    private readonly string currentVersion;
    private ClientIdentity? clientIdentity;
    private TorrentDocument? torrent;
    private CancellationTokenSource? sessionCancellation;
    private DateTimeOffset lastCounterUpdate;
    private DateTimeOffset nextAnnounce;
    private DateTimeOffset sessionStarted;
    private long uploaded;
    private long downloaded;
    private long initialCompletedBytes;
    private bool announcing;
    private bool completionAnnounced;
    private bool closingInProgress;
    private bool allowClose;
    private int sessionGeneration;
    private string availableReleaseUrl = LatestReleaseUrl;
    private ReleaseUpdateResult? availableUpdate;
    private int activeTrackerIndex;

    public MainWindow()
    {
        InitializeComponent();
        settings = ApplicationSettingsStore.Load();
        ActivityList.ItemsSource = activity;
        AnnounceHistoryList.ItemsSource = announceHistory;
        ClientCombo.ItemsSource = ClientProfileCatalog.All;
        ApplySettings();
        PlatformText.Text = $".NET 10 / {GetPlatformName()}";
        currentVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        VersionText.Text = "v" + currentVersion;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += Timer_Tick;
        ResetTransferCounters();
        AddActivity("Ready. Open a torrent file to configure a session.");
        AddDebug($"Application started on {Environment.OSVersion}; log file: {DebugLogStore.DefaultPath}");
        AddDebug($"Runtime: framework={RuntimeInformation.FrameworkDescription}; process_architecture={RuntimeInformation.ProcessArchitecture}; os_architecture={RuntimeInformation.OSArchitecture}; processors={Environment.ProcessorCount}");
        AddDebug($"Network: ipv6_supported={Socket.OSSupportsIPv6}; local_addresses={string.Join(',', NetworkAddressCatalog.GetLocalAddresses())}");
        AddDebug($"Settings: client={settings.DefaultProfileName}; source_address={(string.IsNullOrEmpty(settings.LocalAddress) ? "automatic" : settings.LocalAddress)}; port={settings.Port}; peers={settings.PeerCount}; interval={settings.IntervalSeconds}; proxy={settings.ProxyMode}; random_upload={settings.RandomizeUpload}; random_download={settings.RandomizeDownload}");
        Opened += async (_, _) => await CheckForUpdatesAsync(manual: false);
        Closing += MainWindow_Closing;
        Closed += (_, _) =>
        {
            announceClient.Dispose();
        };
    }

    private void Repository_Click(object? sender, RoutedEventArgs e) => OpenWebPage(RepositoryUrl);

    private void CreateIssue_Click(object? sender, RoutedEventArgs e) => OpenWebPage(NewIssueUrl);

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(manual: true);

    private async void NetworkDiagnostics_Click(object? sender, RoutedEventArgs e)
    {
        var window = new NetworkDiagnosticsWindow(
            torrent?.Trackers ?? [],
            AddressCombo.SelectedIndex > 0 ? AddressCombo.SelectedItem?.ToString() ?? string.Empty : string.Empty,
            settings.ToProxyOptions());
        await window.ShowDialog(this);
    }

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
        ApplicationThemeManager.Apply(settings.ThemeMode);

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
            if (sessionCancellation is not null)
            {
                await EndSessionAsync("Session stopped", "Session stopped before loading another torrent.");
            }

            torrent = loadedTorrent;
            activeTrackerIndex = 0;
            announceHistory.Clear();
            ResetTransferCounters();
            ResetSessionButton.IsEnabled = true;
            TorrentPathBox.Text = torrent.FilePath;
            TorrentNameText.Text = torrent.Name;
            TorrentSizeText.Text = FormatBytes(torrent.TotalSize);
            UpdateTrackerDisplay();
            InfoHashBox.Text = torrent.InfoHash;
            AddActivity($"Loaded {torrent.Name} ({torrent.FileCount} file(s), {FormatBytes(torrent.TotalSize)}).");
            AddDebug($"Torrent loaded: name={torrent.Name}; files={torrent.FileCount}; size_bytes={torrent.TotalSize}; info_hash={torrent.InfoHash}; tracker={torrent.Tracker}; tracker_tiers={torrent.TrackerTiers?.Count ?? 1}; trackers={torrent.Trackers.Count}");
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
            GenerateClientIdentity(profile);
        }
    }

    private void GenerateClientIdentity(ClientProfile profile)
    {
        UserAgentText.Text = profile.UserAgent;
        PeerCountBox.Value = profile.DefaultPeerCount;
        clientIdentity = profile.CreateIdentity();
        ClientKeyBox.Text = clientIdentity.Key;
        PeerIdBox.Text = clientIdentity.PeerId;
        AddDebug($"Client identity generated: client={profile.Name}; key={clientIdentity.Key}; peer_id={clientIdentity.PeerId}");
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
            UploadRateBox.Value = RateRandomizer.NextInteger(
                settings.MinimumUploadRateKib,
                settings.MaximumUploadRateKib);
        }
        if (settings.RandomizeDownload)
        {
            DownloadRateBox.Value = RateRandomizer.NextInteger(
                settings.MinimumDownloadRateKib,
                settings.MaximumDownloadRateKib);
        }

        initialCompletedBytes = (long)(torrent.TotalSize * ((double)(CompletedBox.Value ?? 0) / 100d));
        downloaded = 0;
        completionAnnounced = initialCompletedBytes >= torrent.TotalSize;
        ResetSessionButton.IsEnabled = true;
        sessionStarted = DateTimeOffset.UtcNow;
        lastCounterUpdate = sessionStarted;
        UpdateTransferDisplay(sessionStarted);
        SetSessionActive(true);
        timer.Start();
        AddActivity("Session started.");
        if (settings.StopCondition != SessionStopCondition.Never)
        {
            AddActivity($"Automatic stop enabled: {DescribeStopCondition()}.");
        }

        AddDebug($"Session started: client={((ClientProfile)ClientCombo.SelectedItem).Name}; user_agent={((ClientProfile)ClientCombo.SelectedItem).UserAgent}; completion={CompletedBox.Value:0.##}; initial_completed_bytes={initialCompletedBytes}; upload_kib={UploadRateBox.Value:0}; download_kib={DownloadRateBox.Value:0}; port={PortBox.Value:0}; peers={PeerCountBox.Value:0}; info_hash={torrent.InfoHash}; key={clientIdentity.Key}; peer_id={clientIdentity.PeerId}");
        TrackerAnnounceResult? result = await SendAnnounceAsync("started", sessionCancellation.Token);
        await HandleTrackerRejectionAsync(result);
    }

    private async void ManualUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (sessionCancellation is null || announcing)
        {
            return;
        }

        bool completed = UpdateTransferCounters(DateTimeOffset.UtcNow);
        AddActivity($"Sending manual update: uploaded {FormatBytes(uploaded)}, downloaded {FormatBytes(downloaded)}.");
        AddDebug($"Manual tracker update requested: uploaded={uploaded}; downloaded={downloaded}");
        string eventName = completed ? "completed" : string.Empty;
        completionAnnounced |= completed;
        TrackerAnnounceResult? result = await SendAnnounceAsync(eventName, sessionCancellation.Token);
        await HandleTrackerRejectionAsync(result);
    }

    private async void ResetSession_Click(object? sender, RoutedEventArgs e)
    {
        if (sessionCancellation is not null)
        {
            await EndSessionAsync("Session reset", "Session stopped before reset.");
        }

        if (ClientCombo.SelectedItem is ClientProfile profile)
        {
            GenerateClientIdentity(profile);
        }

        ResetTransferCounters();
        ResetSessionButton.IsEnabled = torrent is not null;
        StatusText.Text = "Session reset";
        AddActivity("Session reset. Counters and client identity were regenerated.");
        AddDebug("Session reset.");
    }

    private async void Stop_Click(object? sender, RoutedEventArgs e)
    {
        await EndSessionAsync("Stopped", "Session stopped.");
    }

    private void StopActiveSession()
    {
        sessionGeneration++;
        announcing = false;
        timer.Stop();
        sessionCancellation?.Cancel();
        sessionCancellation?.Dispose();
        sessionCancellation = null;
        SetSessionActive(false);
        CountdownText.Text = "-";
        nextAnnounce = default;
    }

    private void SetSessionActive(bool active)
    {
        StartButton.IsEnabled = !active;
        StopButton.IsEnabled = active;
        ManualUpdateButton.IsEnabled = active && !announcing;
        ResetSessionButton.IsEnabled = torrent is not null && !announcing;
        OpenTorrentButton.IsEnabled = !active;
        SettingsButton.IsEnabled = !active;
        ClientCombo.IsEnabled = !active;
        AddressCombo.IsEnabled = !active;
        PortBox.IsEnabled = !active;
        PeerCountBox.IsEnabled = !active;
        CompletedBox.IsEnabled = !active;
        IntervalBox.IsEnabled = !active;
    }

    private async Task EndSessionAsync(string status, string activityMessage)
    {
        CancellationTokenSource? cancellation = sessionCancellation;
        if (cancellation is null)
        {
            return;
        }

        timer.Stop();
        cancellation.Cancel();
        bool previousAnnounceFinished = await WaitForActiveAnnounceAsync();
        if (!previousAnnounceFinished)
        {
            sessionGeneration++;
            announcing = false;
        }

        using var stoppedTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        UpdateTransferCounters(DateTimeOffset.UtcNow);
        await SendAnnounceAsync("stopped", stoppedTimeout.Token);
        StopActiveSession();
        StatusText.Text = status;
        AddActivity(activityMessage);
    }

    private async Task<bool> WaitForActiveAnnounceAsync()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (announcing && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        return !announcing;
    }

    private void ResetTransferCounters()
    {
        uploaded = 0;
        downloaded = 0;
        initialCompletedBytes = 0;
        CompletedBox.Value = 0;
        UploadedText.Text = FormatBytes(0);
        DownloadedText.Text = FormatBytes(0);
        CountdownText.Text = "-";
        RatioText.Text = "Ratio -";
        CompletionText.Text = "Completed 0%";
        ElapsedText.Text = "Elapsed 00:00:00";
        completionAnnounced = false;
        sessionStarted = default;
        nextAnnounce = default;
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (sessionCancellation is null)
        {
            timer.Stop();
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool completed = UpdateTransferCounters(now);
        if (completed && !announcing && sessionCancellation is not null)
        {
            completionAnnounced = true;
            TrackerAnnounceResult? completionResult = await SendAnnounceAsync("completed", sessionCancellation.Token);
            if (await HandleTrackerRejectionAsync(completionResult))
            {
                return;
            }
        }

        if (SessionStopEvaluator.ShouldStop(
            settings.StopCondition,
            settings.StopValue,
            now - sessionStarted,
            uploaded,
            downloaded))
        {
            await EndSessionAsync("Stopped automatically", $"Session stopped automatically: {DescribeStopCondition()}.");
            return;
        }

        TimeSpan remaining = nextAnnounce - now;
        CountdownText.Text = remaining > TimeSpan.Zero ? remaining.ToString(@"mm\:ss") : "now";
        if (remaining <= TimeSpan.Zero && !announcing && sessionCancellation is not null)
        {
            TrackerAnnounceResult? result = await SendAnnounceAsync(string.Empty, sessionCancellation.Token);
            await HandleTrackerRejectionAsync(result);
        }
    }

    private bool UpdateTransferCounters(DateTimeOffset now)
    {
        bool wasComplete = torrent is not null && initialCompletedBytes + downloaded >= torrent.TotalSize;
        double seconds = Math.Max(0, (now - lastCounterUpdate).TotalSeconds);
        lastCounterUpdate = now;
        uploaded += (long)((double)(UploadRateBox.Value ?? 0) * 1024d * seconds);
        downloaded += (long)((double)(DownloadRateBox.Value ?? 0) * 1024d * seconds);
        if (torrent is not null)
        {
            downloaded = Math.Min(downloaded, Math.Max(0, torrent.TotalSize - initialCompletedBytes));
        }

        UpdateTransferDisplay(now);
        bool isComplete = torrent is not null && initialCompletedBytes + downloaded >= torrent.TotalSize;
        return !wasComplete && isComplete && !completionAnnounced;
    }

    private void UpdateTransferDisplay(DateTimeOffset now)
    {
        UploadedText.Text = FormatBytes(uploaded);
        DownloadedText.Text = FormatBytes(downloaded);
        double completion = torrent is { TotalSize: > 0 }
            ? Math.Clamp((initialCompletedBytes + downloaded) * 100d / torrent.TotalSize, 0, 100)
            : 0;
        CompletedBox.Value = (decimal)completion;
        CompletionText.Text = $"Completed {completion:0.##}%";
        RatioText.Text = downloaded > 0 ? $"Ratio {uploaded / (double)downloaded:0.###}" : "Ratio -";
        TimeSpan elapsed = sessionStarted == default ? TimeSpan.Zero : now - sessionStarted;
        ElapsedText.Text = $"Elapsed {FormatElapsed(elapsed)}";
    }

    private async Task<TrackerAnnounceResult?> SendAnnounceAsync(string eventName, CancellationToken cancellationToken)
    {
        if (torrent is null || ClientCombo.SelectedItem is not ClientProfile profile || clientIdentity is null || announcing)
        {
            return null;
        }

        int announceGeneration = sessionGeneration;
        announcing = true;
        ManualUpdateButton.IsEnabled = false;
        ResetSessionButton.IsEnabled = false;
        StatusText.Text = "Contacting tracker...";
        try
        {
            IReadOnlyList<string> trackers = torrent.Trackers;
            TrackerAnnounceResult? result = null;
            Exception? finalException = null;
            for (int offset = 0; offset < trackers.Count; offset++)
            {
                int trackerIndex = (activeTrackerIndex + offset) % trackers.Count;
                string trackerUrl = trackers[trackerIndex];
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
                    clientIdentity,
                    GetRemainingBytes(),
                    trackerUrl);
                AddDebug($"Announce sending: event={eventName}; tracker={trackerUrl}; candidate={trackerIndex + 1}/{trackers.Count}; local_ip={options.LocalIp}; uploaded={uploaded}; downloaded={downloaded}; left={options.Left}");
                try
                {
                    result = await announceClient.AnnounceAsync(options, cancellationToken);
                    AddAnnounceHistory(eventName, trackerUrl, result);
                    if (result.IsSuccess)
                    {
                        if (activeTrackerIndex != trackerIndex)
                        {
                            AddActivity($"Tracker failover selected candidate {trackerIndex + 1} of {trackers.Count}.");
                        }

                        activeTrackerIndex = trackerIndex;
                        UpdateTrackerDisplay();
                        break;
                    }

                    AddDebug($"Tracker candidate rejected announce: candidate={trackerIndex + 1}/{trackers.Count}; message={result.Message}");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    finalException = exception;
                    AddAnnounceHistory(eventName, trackerUrl, exception);
                    AddDebug($"Tracker candidate failed: candidate={trackerIndex + 1}/{trackers.Count}; type={exception.GetType().FullName}; message={exception.Message}");
                }
            }

            if (result is null)
            {
                throw finalException ?? new InvalidOperationException("No tracker candidate was available.");
            }

            if (announceGeneration != sessionGeneration)
            {
                return null;
            }

            int interval = result.IntervalSeconds ?? Decimal.ToInt32(IntervalBox.Value ?? 1800);
            interval = Math.Max(30, interval);
            nextAnnounce = DateTimeOffset.UtcNow.AddSeconds(interval);
            AddDebug($"Tracker response: status={FormatOptional(result.HttpStatusCode)}; protocol={result.HttpVersion}; server={EmptyAsUnknown(result.Server)}; content_type={EmptyAsUnknown(result.ContentType)}; content_length={FormatOptional(result.ContentLength)}; latency_ms={result.ElapsedMilliseconds}; final_url={SensitiveDataRedactor.RedactUrl(result.FinalUrl)}");
            AddDebug($"Tracker statistics: complete={FormatOptional(result.Complete)}; incomplete={FormatOptional(result.Incomplete)}; ipv4_peers={FormatOptional(result.Ipv4Peers)}; ipv6_peers={FormatOptional(result.Ipv6Peers)}; interval={FormatOptional(result.IntervalSeconds)}; min_interval={FormatOptional(result.MinimumIntervalSeconds)}");
            StatusText.Text = result.IsSuccess ? "Tracker accepted announce" : "Tracker rejected announce";
            AddActivity($"{(result.IsSuccess ? "OK" : "ERROR")} {result.Message} Next announce in {interval}s.");
            AddDebug($"Announce response: success={result.IsSuccess}; interval={interval}; message={result.Message}");
            return result;
        }
        catch (OperationCanceledException)
        {
            if (announceGeneration == sessionGeneration)
            {
                StatusText.Text = "Cancelled";
            }

            return null;
        }
        catch (Exception exception)
        {
            if (announceGeneration != sessionGeneration)
            {
                return null;
            }

            int retry = Math.Max(30, Decimal.ToInt32(IntervalBox.Value ?? 1800));
            nextAnnounce = DateTimeOffset.UtcNow.AddSeconds(retry);
            StatusText.Text = "Tracker request failed";
            AddActivity("ERROR " + exception.Message);
            AddDebug($"Announce failed: type={exception.GetType().FullName}; message={exception.Message}; details={exception}");
            return null;
        }
        finally
        {
            if (announceGeneration == sessionGeneration)
            {
                announcing = false;
                ManualUpdateButton.IsEnabled = sessionCancellation is { IsCancellationRequested: false };
                ResetSessionButton.IsEnabled = torrent is not null && !announcing;
            }
        }
    }

    private async Task<bool> HandleTrackerRejectionAsync(TrackerAnnounceResult? result)
    {
        if (result is not { IsSuccess: false } || !settings.StopOnTrackerFailure || sessionCancellation is null)
        {
            return false;
        }

        await EndSessionAsync("Stopped after tracker rejection", "Session stopped because the tracker rejected the announce.");
        return true;
    }

    private string DescribeStopCondition() => settings.StopCondition switch
    {
        SessionStopCondition.AfterDuration => $"after {settings.StopValue:0.##} seconds",
        SessionStopCondition.Uploaded => $"after uploading {settings.StopValue:0.##} MiB",
        SessionStopCondition.Downloaded => $"after downloading {settings.StopValue:0.##} MiB",
        SessionStopCondition.Ratio => $"after reaching ratio {settings.StopValue:0.##}",
        _ => "never",
    };

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose || sessionCancellation is null)
        {
            return;
        }

        e.Cancel = true;
        if (closingInProgress)
        {
            return;
        }

        closingInProgress = true;
        await EndSessionAsync("Closing", "Session stopped before application exit.");
        allowClose = true;
        Close();
    }

    private void AddActivity(string message, bool force = false)
    {
        if (!settings.EnableActivityLog && !force)
        {
            return;
        }

        activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  {SensitiveDataRedactor.Redact(message)}");
        while (activity.Count > 200)
        {
            activity.RemoveAt(activity.Count - 1);
        }
    }

    private void AddDebug(string message)
    {
        if (!settings.EnableActivityLog && !settings.EnableDebugLog)
        {
            return;
        }

        string safeMessage = SensitiveDataRedactor.Redact(message);
        if (settings.EnableActivityLog)
        {
            activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  DEBUG {safeMessage}");
            while (activity.Count > 200)
            {
                activity.RemoveAt(activity.Count - 1);
            }
        }

        if (!settings.EnableDebugLog)
        {
            return;
        }

        try
        {
            DebugLogStore.Append(safeMessage);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = "Could not write debug log";
        }
    }

    private void OpenDebugLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = DebugLogStore.EnsureFile();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            AddDebug("Opened debug log: " + path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = "Could not open debug log";
            AddActivity("ERROR " + exception.Message, force: true);
        }
    }

    private async void CopyDiagnostics_Click(object? sender, RoutedEventArgs e)
    {
        var report = new StringBuilder()
            .AppendLine("RatioForge diagnostics")
            .AppendLine($"Version: {currentVersion}")
            .AppendLine($"Generated (UTC): {DateTimeOffset.UtcNow:O}")
            .AppendLine($"OS: {RuntimeInformation.OSDescription}")
            .AppendLine($"Architecture: process={RuntimeInformation.ProcessArchitecture}; OS={RuntimeInformation.OSArchitecture}")
            .AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Theme: {settings.ThemeMode}")
            .AppendLine($"Activity log: {settings.EnableActivityLog}; debug log: {settings.EnableDebugLog}")
            .AppendLine($"Client: {(ClientCombo.SelectedItem as ClientProfile)?.Name ?? "none"}")
            .AppendLine($"Source address: {AddressCombo.SelectedItem ?? "Automatic (IPv4 / IPv6)"}")
            .AppendLine($"Tracker: {SensitiveDataRedactor.RedactUrl(torrent is null || torrent.Trackers.Count == 0 ? null : torrent.Trackers[activeTrackerIndex])}")
            .AppendLine($"Tracker candidates: {torrent?.Trackers.Count ?? 0}")
            .AppendLine($"Session profile: {settings.SelectedSessionProfileName}")
            .AppendLine($"Session active: {sessionCancellation is not null}")
            .AppendLine($"Uploaded: {FormatBytes(uploaded)}; downloaded: {FormatBytes(downloaded)}")
            .AppendLine("Recent activity:");
        foreach (string entry in activity.Take(25))
        {
            report.AppendLine(SensitiveDataRedactor.RedactDiagnostic(entry, torrent?.Name));
        }

        report.AppendLine("Recent announces:");
        foreach (AnnounceHistoryRow entry in announceHistory.Take(15))
        {
            report.AppendLine($"{entry.Time} {entry.Event} {entry.Protocol} {entry.Status} {entry.Latency} {entry.Interval} {entry.Tracker} {SensitiveDataRedactor.Redact(entry.Result)}");
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                throw new InvalidOperationException("Clipboard is unavailable.");
            }

            await clipboard.SetTextAsync(report.ToString());
            StatusText.Text = "Diagnostics copied";
            AddActivity("Diagnostics copied to clipboard.");
            AddDebug("Diagnostics copied to clipboard.");
        }
        catch (Exception exception)
        {
            StatusText.Text = "Could not copy diagnostics";
            AddActivity("ERROR " + exception.Message, force: true);
        }
    }

    private void ClearActivity_Click(object? sender, RoutedEventArgs e)
    {
        activity.Clear();
        announceHistory.Clear();
        AddActivity("Activity cleared.");
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
            availableUpdate = result;
            availableReleaseUrl = result.ReleaseUrl;
            AddDebug($"Update check: current={result.CurrentVersion}; latest={result.LatestVersion}; available={result.IsUpdateAvailable}");
            if (result.IsUpdateAvailable)
            {
                UpdateButton.Content = $"v{result.LatestVersion.ToString(3)} available";
                UpdateButton.IsVisible = true;
                StatusText.Text = "Update available";
                AddActivity($"Update v{result.LatestVersion.ToString(3)} is available.");
                if (manual)
                {
                    await ShowUpdateDialogAsync(
                        "Update available",
                        $"RatioForge v{result.LatestVersion.ToString(3)} is available. You are running v{currentVersion}.",
                        offerReleaseLink: true,
                        result.ReleaseNotes,
                        result.Package is not null && result.Checksum is not null);
                }
            }
            else if (manual)
            {
                StatusText.Text = "RatioForge is up to date";
                AddActivity($"Version {currentVersion} is up to date.");
                await ShowUpdateDialogAsync(
                    "RatioForge is up to date",
                    $"You are running the latest published version: v{currentVersion}.",
                    offerReleaseLink: false,
                    result.ReleaseNotes,
                    canDownload: false);
            }
        }
        catch (Exception exception)
        {
            AddDebug("Update check failed: " + exception.Message);
            if (manual)
            {
                StatusText.Text = "Could not check for updates";
                AddActivity("ERROR Could not check GitHub for the latest release.", force: true);
                await ShowUpdateDialogAsync(
                    "Update check failed",
                    "RatioForge could not contact GitHub. Check your connection and try again.",
                    offerReleaseLink: false,
                    string.Empty,
                    canDownload: false);
            }
        }
    }

    private async Task ShowUpdateDialogAsync(
        string title,
        string message,
        bool offerReleaseLink,
        string releaseNotes,
        bool canDownload)
    {
        var dialog = new UpdateCheckWindow(title, message, offerReleaseLink, releaseNotes, canDownload);
        UpdateDialogAction action = await dialog.ShowDialog<UpdateDialogAction>(this);
        if (action == UpdateDialogAction.OpenRelease)
        {
            OpenWebPage(availableReleaseUrl);
        }
        else if (action == UpdateDialogAction.Download)
        {
            await DownloadUpdateAsync();
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (availableUpdate is null)
        {
            return;
        }

        StatusText.Text = "Downloading update...";
        AddActivity("Downloading the platform-specific update package.");
        try
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string path = await updateDownloader.DownloadAndVerifyAsync(availableUpdate, downloads);
            StatusText.Text = "Update downloaded and verified";
            AddActivity($"Update downloaded and SHA256 verified: {Path.GetFileName(path)}.");
            AddDebug($"Verified update package: file={path}; version={availableUpdate.LatestVersion}");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            StatusText.Text = "Update download failed";
            AddActivity("ERROR " + exception.Message, force: true);
            AddDebug($"Update download failed: type={exception.GetType().FullName}; message={exception.Message}");
        }
    }

    private void AddAnnounceHistory(string eventName, string trackerUrl, TrackerAnnounceResult result)
    {
        string protocol = new Uri(trackerUrl).Scheme.ToUpperInvariant();
        string status = result.HttpStatusCode?.ToString() ?? (result.IsSuccess ? "OK" : "Error");
        announceHistory.Insert(0, new AnnounceHistoryRow(
            DateTime.Now.ToString("HH:mm:ss"),
            string.IsNullOrWhiteSpace(eventName) ? "update" : eventName,
            SensitiveDataRedactor.RedactUrl(trackerUrl),
            protocol,
            status,
            $"{result.ElapsedMilliseconds} ms",
            result.IntervalSeconds is int interval ? $"{interval}s" : "-",
            SensitiveDataRedactor.Redact(result.Message)));
        TrimAnnounceHistory();
    }

    private void AddAnnounceHistory(string eventName, string trackerUrl, Exception exception)
    {
        announceHistory.Insert(0, new AnnounceHistoryRow(
            DateTime.Now.ToString("HH:mm:ss"),
            string.IsNullOrWhiteSpace(eventName) ? "update" : eventName,
            SensitiveDataRedactor.RedactUrl(trackerUrl),
            new Uri(trackerUrl).Scheme.ToUpperInvariant(),
            "Error",
            "-",
            "-",
            SensitiveDataRedactor.Redact(exception.Message)));
        TrimAnnounceHistory();
    }

    private void TrimAnnounceHistory()
    {
        while (announceHistory.Count > 500)
        {
            announceHistory.RemoveAt(announceHistory.Count - 1);
        }
    }

    private void UpdateTrackerDisplay()
    {
        if (torrent is null || torrent.Trackers.Count == 0)
        {
            TrackerText.Text = "-";
            return;
        }

        activeTrackerIndex = Math.Clamp(activeTrackerIndex, 0, torrent.Trackers.Count - 1);
        TrackerText.Text = torrent.Trackers.Count == 1
            ? torrent.Trackers[activeTrackerIndex]
            : $"{torrent.Trackers[activeTrackerIndex]} ({activeTrackerIndex + 1}/{torrent.Trackers.Count})";
    }

    private static string FormatLocalAddresses(IEnumerable<string> addresses, AddressFamily family)
    {
        string value = string.Join(", ", addresses.Where(address =>
            IPAddress.TryParse(address, out IPAddress? parsed) && parsed.AddressFamily == family));
        return string.IsNullOrEmpty(value) ? "Not available" : value;
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

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";

    private static string FormatOptional<T>(T? value) where T : struct => value?.ToString() ?? "not-provided";

    private static string EmptyAsUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "not-provided" : value;

    private long GetRemainingBytes() => torrent is null
        ? 0
        : Math.Max(0, torrent.TotalSize - initialCompletedBytes - downloaded);

    private static string GetPlatformName() =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        OperatingSystem.IsLinux() ? "Linux" : "Desktop";

}

public sealed record AnnounceHistoryRow(
    string Time,
    string Event,
    string Tracker,
    string Protocol,
    string Status,
    string Latency,
    string Interval,
    string Result);
