namespace RatioForge.Desktop;

using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class SettingsWindow : Window
{
    private const string AutomaticAddress = "Automatic (IPv4 / IPv6)";
    private readonly ApplicationSettings settings;

    public SettingsWindow()
        : this(new ApplicationSettings())
    {
    }

    public SettingsWindow(ApplicationSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
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
    }

    private void ProxyModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => UpdateProxyFields();

    private void UpdateProxyFields()
    {
        bool enabled = ProxyModeCombo.SelectedItem is TrackerProxyMode.Http or TrackerProxyMode.Socks5;
        ProxyHostBox.IsEnabled = enabled;
        ProxyPortBox.IsEnabled = enabled;
        ProxyUsernameBox.IsEnabled = enabled;
        ProxyPasswordBox.IsEnabled = enabled;
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
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
        settings.Normalize();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
