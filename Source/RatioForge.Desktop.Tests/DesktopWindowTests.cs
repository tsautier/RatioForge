namespace RatioForge.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Styling;
using NUnit.Framework;

public sealed class DesktopWindowTests
{
    [AvaloniaTest]
    public void MainWindowShouldStartWithEmptyCountersAndUnavailableSessionActions()
    {
        var window = new MainWindow();
        IEnumerable<string> activity = window.FindControl<ListBox>("ActivityList")!.ItemsSource!.Cast<string>();

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBlock>("UploadedText")?.Text, Is.EqualTo("0 B"));
            Assert.That(window.FindControl<TextBlock>("DownloadedText")?.Text, Is.EqualTo("0 B"));
            Assert.That(window.FindControl<TextBlock>("CountdownText")?.Text, Is.EqualTo("-"));
            Assert.That(window.FindControl<TextBlock>("RatioText")?.Text, Is.EqualTo("Ratio -"));
            Assert.That(window.FindControl<TextBlock>("CompletionText")?.Text, Is.EqualTo("Completed 0%"));
            Assert.That(window.FindControl<TextBlock>("ElapsedText")?.Text, Is.EqualTo("Elapsed 00:00:00"));
            Assert.That(window.FindControl<Button>("ManualUpdateButton")?.IsEnabled, Is.False);
            Assert.That(window.FindControl<Button>("ResetSessionButton")?.IsEnabled, Is.False);
            Assert.That(window.FindControl<Button>("RetryTrackerButton")?.IsEnabled, Is.False);
            Assert.That(activity.Any(entry => entry.Contains("DEBUG Application started", StringComparison.Ordinal)), Is.True);
        });
    }

    [AvaloniaTest]
    public void SettingsWindowShouldExposeSessionSafetyDefaults()
    {
        var window = new SettingsWindow(new ApplicationSettings());

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<CheckBox>("StopOnTrackerFailureCheck")?.IsChecked, Is.True);
            Assert.That(window.FindControl<CheckBox>("PauseUploadWhenNoLeechersCheck")?.IsChecked, Is.True);
            Assert.That(window.FindControl<ComboBox>("StopConditionCombo")?.SelectedIndex, Is.Zero);
            Assert.That(window.FindControl<NumericUpDown>("StopValueBox")?.IsEnabled, Is.False);
        });
    }

    [AvaloniaTest]
    public void SettingsWindowShouldRejectInvertedRandomizationBoundsImmediately()
    {
        var window = new SettingsWindow(new ApplicationSettings());
        NumericUpDown minimum = window.FindControl<NumericUpDown>("MinimumUploadBox")!;
        NumericUpDown maximum = window.FindControl<NumericUpDown>("MaximumUploadBox")!;

        minimum.Value = 100;
        maximum.Value = 10;

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBlock>("ValidationText")?.IsVisible, Is.True);
            Assert.That(window.FindControl<Button>("SaveButton")?.IsEnabled, Is.False);
        });

        maximum.Value = 100;
        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<TextBlock>("ValidationText")?.IsVisible, Is.False);
            Assert.That(window.FindControl<Button>("SaveButton")?.IsEnabled, Is.True);
        });
    }

    [TestCase(ApplicationThemeMode.System)]
    [TestCase(ApplicationThemeMode.Light)]
    [TestCase(ApplicationThemeMode.Dark)]
    public void ThemeManagerShouldResolveEveryAppearanceMode(ApplicationThemeMode mode)
    {
        ThemeVariant expected = mode switch
        {
            ApplicationThemeMode.Light => ThemeVariant.Light,
            ApplicationThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        Assert.That(ApplicationThemeManager.Resolve(mode), Is.SameAs(expected));
    }

    [AvaloniaTest]
    public void UpdateDialogShouldOnlyOfferReleaseLinkWhenAnUpdateExists()
    {
        var available = new UpdateCheckWindow("Update available", "Version 2 is available.", true);
        var current = new UpdateCheckWindow("Up to date", "Latest version installed.", false);

        Assert.Multiple(() =>
        {
            Assert.That(available.FindControl<Button>("OpenReleaseButton")?.IsVisible, Is.True);
            Assert.That(current.FindControl<Button>("OpenReleaseButton")?.IsVisible, Is.False);
        });
    }

    [AvaloniaTest]
    public void MainWindowShouldExposeStructuredAnnounceHistory()
    {
        var window = new MainWindow();

        Assert.Multiple(() =>
        {
            Assert.That(window.FindControl<ListBox>("AnnounceHistoryList"), Is.Not.Null);
            Assert.That(window.FindControl<ListBox>("AnnounceHistoryList")?.ItemsSource, Is.Not.Null);
            Assert.That(window.FindControl<ComboBox>("HistoryEventFilter"), Is.Not.Null);
            Assert.That(window.FindControl<ComboBox>("HistoryProtocolFilter"), Is.Not.Null);
            Assert.That(window.FindControl<ComboBox>("HistoryStatusFilter"), Is.Not.Null);
        });
    }

    [Test]
    public void HistoryExportsShouldRedactSecretsAndRemainMachineReadable()
    {
        AnnounceHistoryRow[] entries =
        [
            new("12:00:00", "started", "https://tracker.example/abcdef0123456789abcdef0123456789/announce?token=secret", "HTTPS", "200", "10 ms", "900s", "key=ABC peer_id=private"),
        ];

        string csv = AnnounceHistoryExporter.ToCsv(entries);
        string json = AnnounceHistoryExporter.ToJson(entries);

        Assert.Multiple(() =>
        {
            Assert.That(csv, Does.Contain("Time,Event,Tracker"));
            Assert.That(csv, Does.Not.Contain("abcdef0123456789abcdef0123456789"));
            Assert.That(csv, Does.Not.Contain("secret"));
            Assert.That(json, Does.Contain("REDACTED"));
            Assert.That(json, Does.Not.Contain("peer_id=private"));
        });
    }

    [AvaloniaTest]
    public void SettingsAndUpdateWindowsShouldExposeProfilesAndVerifiedDownload()
    {
        var settings = new SettingsWindow(new ApplicationSettings());
        var update = new UpdateCheckWindow(
            "Update available", "Version 2 is available.", true, "Release notes", canDownload: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.FindControl<ListBox>("SessionProfilesList"), Is.Not.Null);
            Assert.That(settings.FindControl<TextBox>("SessionProfileNameBox"), Is.Not.Null);
            Assert.That(update.FindControl<Button>("DownloadButton")?.IsVisible, Is.True);
            Assert.That(update.FindControl<TextBox>("ReleaseNotesText")?.Text, Is.EqualTo("Release notes"));
        });
    }
}
