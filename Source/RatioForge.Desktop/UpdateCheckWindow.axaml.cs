namespace RatioForge.Desktop;

using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class UpdateCheckWindow : Window
{
    public UpdateCheckWindow()
        : this("Update check", string.Empty, offerReleaseLink: false)
    {
    }

    public UpdateCheckWindow(string title, string message, bool offerReleaseLink)
    {
        InitializeComponent();
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;
        OpenReleaseButton.IsVisible = offerReleaseLink;
    }

    private void OpenRelease_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);
}
