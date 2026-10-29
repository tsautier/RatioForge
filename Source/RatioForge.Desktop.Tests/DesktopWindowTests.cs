namespace RatioForge.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using NUnit.Framework;
using System.Reflection;

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
            Assert.That(window.FindControl<TextBox>("HistoryRequestBox"), Is.Not.Null);
            Assert.That(window.FindControl<TextBox>("HistoryDiagnosticBox"), Is.Not.Null);
            Assert.That(window.FindControl<Button>("CopyAnnounceButton")?.IsEnabled, Is.False);
        });
    }

    [AvaloniaTest]
    public void TrackerSelectionShouldNotReenterDisplayRefresh()
    {
        const string primary = "https://primary.example/announce";
        const string secondary = "https://secondary.example/announce";
        var torrent = new TorrentDocument(
            "fixture.torrent", "Fixture", primary, new string('a', 40), 1024, 1,
            [[primary, secondary]]);
        var window = new MainWindow();
        typeof(MainWindow).GetField("torrent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, torrent);
        typeof(MainWindow).GetMethod("UpdateTrackerDisplay", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, null);
        ComboBox trackerCombo = window.FindControl<ComboBox>("TrackerCombo")!;

        trackerCombo.SelectedIndex = 1;

        Assert.Multiple(() =>
        {
            Assert.That(trackerCombo.SelectedIndex, Is.EqualTo(1));
            Assert.That(window.FindControl<TextBlock>("TrackerText")?.Text,
                Is.EqualTo($"{secondary} (2/2)"));
        });
    }

    [AvaloniaTest]
    public void UnexpectedHandlerFailureShouldBeReportedWithoutCrashing()
    {
        var window = new MainWindow();
        Task guarded = InvokePrivate<Task>(window, "GuardAsync", "update the session", (Func<Task>)(() =>
            throw new InvalidOperationException("Tracker https://tracker.example/announce?passkey=secret123 exploded")));
        PumpUntil(() => guarded.IsCompleted);
        IEnumerable<string> activity = window.FindControl<ListBox>("ActivityList")!.ItemsSource!.Cast<string>();

        Assert.Multiple(() =>
        {
            Assert.That(guarded.IsCompletedSuccessfully, Is.True);
            Assert.That(window.FindControl<TextBlock>("StatusText")?.Text, Is.EqualTo("Unexpected error"));
            Assert.That(activity.Any(entry => entry.Contains("ERROR Could not update the session", StringComparison.Ordinal)), Is.True);
            Assert.That(activity.Any(entry => entry.Contains("secret123", StringComparison.Ordinal)), Is.False);
        });
    }

    [AvaloniaTest]
    public void ConcurrentStopRequestsShouldShareOneShutdownAndReleaseControls()
    {
        var window = new MainWindow();
        SetPrivate(window, "sessionCancellation", new CancellationTokenSource());
        SetPrivate(window, "announcing", true);

        Task first = InvokePrivate<Task>(window, "EndSessionAsync", "Stopped", "Session stopped.");
        Task second = InvokePrivate<Task>(window, "EndSessionAsync", "Stopped automatically", "Session stopped automatically.");
        SetPrivate(window, "announcing", false);
        PumpUntil(() => first.IsCompleted);
        IEnumerable<string> activity = window.FindControl<ListBox>("ActivityList")!.ItemsSource!.Cast<string>();

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(first.IsCompletedSuccessfully, Is.True);
            Assert.That(GetPrivate(window, "sessionCancellation"), Is.Null);
            Assert.That(window.FindControl<Button>("StartButton")?.IsEnabled, Is.True);
            Assert.That(window.FindControl<Button>("StopButton")?.IsEnabled, Is.False);
            Assert.That(activity.Count(entry => entry.EndsWith("Session stopped.", StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(activity.Any(entry => entry.Contains("Session stopped automatically", StringComparison.Ordinal)), Is.False);
        });
    }

    [AvaloniaTest]
    public void ClosingDuringActiveSessionShouldStopSessionThenClose()
    {
        var window = new MainWindow();
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        SetPrivate(window, "sessionCancellation", new CancellationTokenSource());

        window.Close();
        PumpUntil(() => closed);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.True);
            Assert.That(GetPrivate(window, "sessionCancellation"), Is.Null);
        });
    }

    [Test]
    public void HistoryFilterShouldCombineEventProtocolAndOutcome()
    {
        AnnounceHistoryRow[] entries =
        [
            new("12:00:00", "started", "https://a.example", "HTTPS", "200", "5 ms", "900s", "OK"),
            new("12:01:00", "update", "udp://b.example", "UDP", "OK", "7 ms", "900s", "OK"),
            new("12:02:00", "update", "https://a.example", "HTTPS", "Error", "-", "-", "timeout"),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(new AnnounceHistoryFilter().Apply(entries).Count(), Is.EqualTo(3));
            Assert.That(new AnnounceHistoryFilter(Event: "UPDATE").Apply(entries).Count(), Is.EqualTo(2));
            Assert.That(new AnnounceHistoryFilter(Status: "Success").Apply(entries).Select(e => e.Time),
                Is.EqualTo(new[] { "12:00:00", "12:01:00" }));
            Assert.That(new AnnounceHistoryFilter(Protocol: "HTTPS", Status: "Error").Apply(entries).Single().Result,
                Is.EqualTo("timeout"));
        });
    }

    [Test]
    public void HistoryExportsShouldRedactSecretsAndRemainMachineReadable()
    {
        AnnounceHistoryRow[] entries =
        [
            new("12:00:00", "started", "https://tracker.example/abcdef0123456789abcdef0123456789/announce?token=secret", "HTTPS", "200", "10 ms", "900s", "key=ABC peer_id=private", 2,
                "https://tracker.example/abcdef0123456789abcdef0123456789/announce?info_hash=private&peer_id=private&key=private",
                "candidate=2/3; proxy_username=private", "https://cdn.example/abcdef0123456789abcdef0123456789"),
        ];

        string csv = AnnounceHistoryExporter.ToCsv(entries);
        string json = AnnounceHistoryExporter.ToJson(entries);

        Assert.Multiple(() =>
        {
            Assert.That(csv, Does.Contain("Time,Attempt,Event,Tracker"));
            Assert.That(csv, Does.Not.Contain("abcdef0123456789abcdef0123456789"));
            Assert.That(csv, Does.Not.Contain("secret"));
            Assert.That(json, Does.Contain("REDACTED"));
            Assert.That(json, Does.Not.Contain("peer_id=private"));
            Assert.That(json, Does.Not.Contain("proxy_username=private"));
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
            Assert.That(settings.FindControl<Button>("ImportSessionProfilesButton"), Is.Not.Null);
            Assert.That(settings.FindControl<Button>("ExportSessionProfilesButton"), Is.Not.Null);
            Assert.That(settings.FindControl<Button>("ImportClientProfilesButton"), Is.Not.Null);
            Assert.That(settings.FindControl<Button>("ExportClientProfilesButton"), Is.Not.Null);
            Assert.That(update.FindControl<Button>("DownloadButton")?.IsVisible, Is.True);
            Assert.That(update.FindControl<TextBox>("ReleaseNotesText")?.Text, Is.EqualTo("Release notes"));
        });
    }

    private static T InvokePrivate<T>(object target, string name, params object[] arguments) =>
        (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments)!;

    private static object? GetPrivate(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);

    private static void SetPrivate(object target, string name, object? value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void PumpUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
