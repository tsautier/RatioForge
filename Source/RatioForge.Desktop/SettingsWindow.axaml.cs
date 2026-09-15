namespace RatioForge.Desktop;

using System.Diagnostics;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class SettingsWindow : Window
{
    private const string AutomaticAddress = "Automatic (IPv4 / IPv6)";
    private static readonly StopConditionOption[] StopConditionOptions =
    [
        new(SessionStopCondition.Never, "Never", string.Empty),
        new(SessionStopCondition.AfterDuration, "After a duration", "Seconds"),
        new(SessionStopCondition.Uploaded, "After uploaded volume", "MiB"),
        new(SessionStopCondition.Downloaded, "After downloaded volume", "MiB"),
        new(SessionStopCondition.Ratio, "After reaching a ratio", "Ratio"),
    ];
    private readonly ApplicationSettings settings;
    private readonly ObservableCollection<SessionProfile> sessionProfiles;

    public SettingsWindow()
        : this(new ApplicationSettings())
    {
    }

    public SettingsWindow(ApplicationSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        sessionProfiles = new ObservableCollection<SessionProfile>(SessionProfileStore.Load());
        SessionProfilesList.ItemsSource = sessionProfiles;
        ThemeModeCombo.ItemsSource = Enum.GetValues<ApplicationThemeMode>();
        ThemeModeCombo.SelectedItem = settings.ThemeMode;
        ProfileCombo.ItemsSource = ClientProfileCatalog.All;
        ProfileCombo.SelectedItem = ClientProfileCatalog.All.First(
            profile => profile.Name == settings.DefaultProfileName);

        var addresses = new List<string> { AutomaticAddress };
        addresses.AddRange(NetworkAddressCatalog.GetLocalAddresses());
        if (!string.IsNullOrWhiteSpace(settings.LocalAddress) && !addresses.Contains(settings.LocalAddress, StringComparer.Ordinal))
        {
            addresses.Add(settings.LocalAddress);
        }

        AddressCombo.ItemsSource = addresses;
        AddressCombo.SelectedItem = string.IsNullOrWhiteSpace(settings.LocalAddress)
            ? AutomaticAddress
            : settings.LocalAddress;
        PortBox.Value = settings.Port;
        PeerCountBox.Value = settings.PeerCount;
        UploadRateBox.Value = settings.UploadRateKib;
        DownloadRateBox.Value = settings.DownloadRateKib;
        IntervalBox.Value = settings.IntervalSeconds;
        ActivityLogCheck.IsChecked = settings.EnableActivityLog;
        DebugLogCheck.IsChecked = settings.EnableDebugLog;
        StopOnTrackerFailureCheck.IsChecked = settings.StopOnTrackerFailure;
        StopConditionCombo.ItemsSource = StopConditionOptions;
        StopConditionCombo.SelectedItem = StopConditionOptions.First(
            option => option.Condition == settings.StopCondition);
        StopValueBox.Value = settings.StopValue;
        DebugLogPathBox.Text = DebugLogStore.DefaultPath;
        RandomUploadCheck.IsChecked = settings.RandomizeUpload;
        MinimumUploadBox.Value = settings.MinimumUploadRateKib;
        MaximumUploadBox.Value = settings.MaximumUploadRateKib;
        RandomDownloadCheck.IsChecked = settings.RandomizeDownload;
        MinimumDownloadBox.Value = settings.MinimumDownloadRateKib;
        MaximumDownloadBox.Value = settings.MaximumDownloadRateKib;
        ProxyModeCombo.ItemsSource = Enum.GetValues<TrackerProxyMode>();
        ProxyModeCombo.SelectedItem = settings.ProxyMode;
        ProxyHostBox.Text = settings.ProxyHost;
        ProxyPortBox.Value = settings.ProxyPort;
        ProxyUsernameBox.Text = settings.ProxyUsername;
        ProxyPasswordBox.Text = settings.ProxyPassword;
        UpdateProxyFields();
        UpdateStopConditionFields();
        MinimumUploadBox.ValueChanged += RandomBounds_ValueChanged;
        MaximumUploadBox.ValueChanged += RandomBounds_ValueChanged;
        MinimumDownloadBox.ValueChanged += RandomBounds_ValueChanged;
        MaximumDownloadBox.ValueChanged += RandomBounds_ValueChanged;
        ValidateRandomBounds();
        SessionProfilesList.SelectedItem = sessionProfiles.FirstOrDefault(profile =>
            profile.Name.Equals(settings.SelectedSessionProfileName, StringComparison.OrdinalIgnoreCase));
    }

    private void ProxyModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateProxyFields();

    private void StopConditionCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UpdateStopConditionFields();

    private void UpdateProxyFields()
    {
        bool enabled = ProxyModeCombo.SelectedItem is TrackerProxyMode.Http or TrackerProxyMode.Socks5;
        ProxyHostBox.IsEnabled = enabled;
        ProxyPortBox.IsEnabled = enabled;
        ProxyUsernameBox.IsEnabled = enabled;
        ProxyPasswordBox.IsEnabled = enabled;
    }

    private void UpdateStopConditionFields()
    {
        StopConditionOption? option = StopConditionCombo.SelectedItem as StopConditionOption;
        StopValueBox.IsEnabled = option?.Condition != SessionStopCondition.Never;
        StopValueLabel.Text = option?.Unit ?? string.Empty;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (!ValidateRandomBounds())
        {
            return;
        }

        settings.ThemeMode = ThemeModeCombo.SelectedItem is ApplicationThemeMode themeMode
            ? themeMode
            : ApplicationThemeMode.System;
        settings.DefaultProfileName = (ProfileCombo.SelectedItem as ClientProfile)?.Name
            ?? ClientProfileCatalog.DefaultProfileName;
        settings.LocalAddress = AddressCombo.SelectedIndex > 0
            ? AddressCombo.SelectedItem?.ToString() ?? string.Empty
            : string.Empty;
        settings.Port = Decimal.ToInt32(PortBox.Value ?? 6881);
        settings.PeerCount = Decimal.ToInt32(PeerCountBox.Value ?? 200);
        settings.UploadRateKib = UploadRateBox.Value ?? 60;
        settings.DownloadRateKib = DownloadRateBox.Value ?? 30;
        settings.IntervalSeconds = Decimal.ToInt32(IntervalBox.Value ?? 1800);
        settings.EnableActivityLog = ActivityLogCheck.IsChecked == true;
        settings.EnableDebugLog = DebugLogCheck.IsChecked == true;
        settings.StopOnTrackerFailure = StopOnTrackerFailureCheck.IsChecked == true;
        settings.StopCondition = (StopConditionCombo.SelectedItem as StopConditionOption)?.Condition
            ?? SessionStopCondition.Never;
        settings.StopValue = StopValueBox.Value ?? 3600;
        settings.RandomizeUpload = RandomUploadCheck.IsChecked == true;
        settings.MinimumUploadRateKib = MinimumUploadBox.Value ?? 40;
        settings.MaximumUploadRateKib = MaximumUploadBox.Value ?? 80;
        settings.RandomizeDownload = RandomDownloadCheck.IsChecked == true;
        settings.MinimumDownloadRateKib = MinimumDownloadBox.Value ?? 20;
        settings.MaximumDownloadRateKib = MaximumDownloadBox.Value ?? 40;
        settings.ProxyMode = ProxyModeCombo.SelectedItem is TrackerProxyMode mode ? mode : TrackerProxyMode.None;
        settings.ProxyHost = ProxyHostBox.Text ?? string.Empty;
        settings.ProxyPort = Decimal.ToInt32(ProxyPortBox.Value ?? 8080);
        settings.ProxyUsername = ProxyUsernameBox.Text ?? string.Empty;
        settings.ProxyPassword = ProxyPasswordBox.Text ?? string.Empty;
        settings.SelectedSessionProfileName = (SessionProfilesList.SelectedItem as SessionProfile)?.Name ?? string.Empty;
        settings.Normalize();
        Close(true);
    }

    private void LoadProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (SessionProfilesList.SelectedItem is not SessionProfile profile)
        {
            ShowValidation("Select a profile to load.");
            return;
        }

        var snapshot = new ApplicationSettings();
        profile.ApplyTo(snapshot);
        ApplySessionControls(snapshot);
        SessionProfileNameBox.Text = profile.Name;
        HideValidation();
    }

    private void SaveProfile_Click(object? sender, RoutedEventArgs e)
    {
        string name = SessionProfileNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowValidation("Enter a profile name.");
            return;
        }

        if (!ValidateRandomBounds())
        {
            return;
        }

        SessionProfile profile = SessionProfile.FromSettings(name, CaptureSessionControls());
        SessionProfile? existing = sessionProfiles.FirstOrDefault(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            int index = sessionProfiles.IndexOf(existing);
            sessionProfiles[index] = profile;
        }
        else
        {
            sessionProfiles.Add(profile);
        }

        SessionProfileStore.Save(sessionProfiles);
        SessionProfilesList.SelectedItem = profile;
        HideValidation();
    }

    private void DeleteProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (SessionProfilesList.SelectedItem is not SessionProfile profile)
        {
            ShowValidation("Select a profile to delete.");
            return;
        }

        sessionProfiles.Remove(profile);
        SessionProfileStore.Save(sessionProfiles);
        SessionProfileNameBox.Text = string.Empty;
        HideValidation();
    }

    private ApplicationSettings CaptureSessionControls()
    {
        var snapshot = new ApplicationSettings
        {
            DefaultProfileName = (ProfileCombo.SelectedItem as ClientProfile)?.Name ?? ClientProfileCatalog.DefaultProfileName,
            LocalAddress = AddressCombo.SelectedIndex > 0 ? AddressCombo.SelectedItem?.ToString() ?? string.Empty : string.Empty,
            Port = Decimal.ToInt32(PortBox.Value ?? 6881),
            PeerCount = Decimal.ToInt32(PeerCountBox.Value ?? 200),
            UploadRateKib = UploadRateBox.Value ?? 60,
            DownloadRateKib = DownloadRateBox.Value ?? 30,
            IntervalSeconds = Decimal.ToInt32(IntervalBox.Value ?? 1800),
            RandomizeUpload = RandomUploadCheck.IsChecked == true,
            MinimumUploadRateKib = MinimumUploadBox.Value ?? 40,
            MaximumUploadRateKib = MaximumUploadBox.Value ?? 80,
            RandomizeDownload = RandomDownloadCheck.IsChecked == true,
            MinimumDownloadRateKib = MinimumDownloadBox.Value ?? 20,
            MaximumDownloadRateKib = MaximumDownloadBox.Value ?? 40,
            ProxyMode = ProxyModeCombo.SelectedItem is TrackerProxyMode mode ? mode : TrackerProxyMode.None,
            ProxyHost = ProxyHostBox.Text ?? string.Empty,
            ProxyPort = Decimal.ToInt32(ProxyPortBox.Value ?? 8080),
            ProxyUsername = ProxyUsernameBox.Text ?? string.Empty,
        };
        snapshot.Normalize();
        return snapshot;
    }

    private void ApplySessionControls(ApplicationSettings source)
    {
        ProfileCombo.SelectedItem = ClientProfileCatalog.All.First(profile => profile.Name == source.DefaultProfileName);
        AddressCombo.SelectedItem = string.IsNullOrWhiteSpace(source.LocalAddress) ? AutomaticAddress : source.LocalAddress;
        PortBox.Value = source.Port;
        PeerCountBox.Value = source.PeerCount;
        UploadRateBox.Value = source.UploadRateKib;
        DownloadRateBox.Value = source.DownloadRateKib;
        IntervalBox.Value = source.IntervalSeconds;
        RandomUploadCheck.IsChecked = source.RandomizeUpload;
        MinimumUploadBox.Value = source.MinimumUploadRateKib;
        MaximumUploadBox.Value = source.MaximumUploadRateKib;
        RandomDownloadCheck.IsChecked = source.RandomizeDownload;
        MinimumDownloadBox.Value = source.MinimumDownloadRateKib;
        MaximumDownloadBox.Value = source.MaximumDownloadRateKib;
        ProxyModeCombo.SelectedItem = source.ProxyMode;
        ProxyHostBox.Text = source.ProxyHost;
        ProxyPortBox.Value = source.ProxyPort;
        ProxyUsernameBox.Text = source.ProxyUsername;
        ProxyPasswordBox.Text = string.Empty;
        UpdateProxyFields();
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.IsVisible = true;
    }

    private void HideValidation()
    {
        ValidationText.Text = string.Empty;
        ValidationText.IsVisible = false;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void ResetDefaults_Click(object? sender, RoutedEventArgs e)
    {
        var defaults = new ApplicationSettings();
        ThemeModeCombo.SelectedItem = defaults.ThemeMode;
        ProfileCombo.SelectedItem = ClientProfileCatalog.Default;
        AddressCombo.SelectedItem = AutomaticAddress;
        PortBox.Value = defaults.Port;
        PeerCountBox.Value = defaults.PeerCount;
        UploadRateBox.Value = defaults.UploadRateKib;
        DownloadRateBox.Value = defaults.DownloadRateKib;
        IntervalBox.Value = defaults.IntervalSeconds;
        ActivityLogCheck.IsChecked = defaults.EnableActivityLog;
        DebugLogCheck.IsChecked = defaults.EnableDebugLog;
        StopOnTrackerFailureCheck.IsChecked = defaults.StopOnTrackerFailure;
        StopConditionCombo.SelectedItem = StopConditionOptions.First(
            option => option.Condition == defaults.StopCondition);
        StopValueBox.Value = defaults.StopValue;
        RandomUploadCheck.IsChecked = defaults.RandomizeUpload;
        MinimumUploadBox.Value = defaults.MinimumUploadRateKib;
        MaximumUploadBox.Value = defaults.MaximumUploadRateKib;
        RandomDownloadCheck.IsChecked = defaults.RandomizeDownload;
        MinimumDownloadBox.Value = defaults.MinimumDownloadRateKib;
        MaximumDownloadBox.Value = defaults.MaximumDownloadRateKib;
        ProxyModeCombo.SelectedItem = defaults.ProxyMode;
        ProxyHostBox.Text = defaults.ProxyHost;
        ProxyPortBox.Value = defaults.ProxyPort;
        ProxyUsernameBox.Text = defaults.ProxyUsername;
        ProxyPasswordBox.Text = string.Empty;
        SessionProfilesList.SelectedItem = null;
        SessionProfileNameBox.Text = string.Empty;
        UpdateProxyFields();
        UpdateStopConditionFields();
        ValidateRandomBounds();
    }

    private void RandomBounds_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        ValidateRandomBounds();

    private bool ValidateRandomBounds()
    {
        bool uploadValid = (MinimumUploadBox.Value ?? 0) <= (MaximumUploadBox.Value ?? 0);
        bool downloadValid = (MinimumDownloadBox.Value ?? 0) <= (MaximumDownloadBox.Value ?? 0);
        ValidationText.Text = !uploadValid && !downloadValid
            ? "Upload and download minimums must not exceed their maximums."
            : !uploadValid
                ? "Upload minimum must not exceed its maximum."
                : !downloadValid
                    ? "Download minimum must not exceed its maximum."
                    : string.Empty;
        ValidationText.IsVisible = !uploadValid || !downloadValid;
        SaveButton.IsEnabled = uploadValid && downloadValid;
        return uploadValid && downloadValid;
    }

    private void OpenDebugLog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string path = DebugLogStore.EnsureFile();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ValidationText.Text = "Could not open the debug log: " + exception.Message;
            ValidationText.IsVisible = true;
        }
    }

    private sealed record StopConditionOption(
        SessionStopCondition Condition,
        string Label,
        string Unit)
    {
        public override string ToString() => Label;
    }
}
