namespace RatioForge.Desktop;

using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class UpdateCheckWindow : Window
{
    public UpdateCheckWindow()
        : this("Update check", string.Empty, offerReleaseLink: false, string.Empty, canDownload: false)
    {
    }

    public UpdateCheckWindow(
        string title,
        string message,
        bool offerReleaseLink,
        string releaseNotes = "",
        bool canDownload = false)
    {
        InitializeComponent();
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;
        OpenReleaseButton.IsVisible = offerReleaseLink;
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(releaseNotes) ? "No release notes were provided." : releaseNotes;
        ReleaseNotesText.IsVisible = !string.IsNullOrWhiteSpace(releaseNotes);
        DownloadButton.IsVisible = canDownload;
    }

    private void OpenRelease_Click(object? sender, RoutedEventArgs e) => Close(UpdateDialogAction.OpenRelease);

    private void Download_Click(object? sender, RoutedEventArgs e) => Close(UpdateDialogAction.Download);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(UpdateDialogAction.Close);
}

public enum UpdateDialogAction
{
    Close,
    OpenRelease,
    Download,
}
