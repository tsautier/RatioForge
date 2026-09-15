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
}
