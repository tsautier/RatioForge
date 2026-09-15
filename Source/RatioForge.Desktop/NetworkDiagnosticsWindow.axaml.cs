namespace RatioForge.Desktop;

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

public partial class NetworkDiagnosticsWindow : Window
{
    private readonly IReadOnlyList<string> trackers;
    private readonly string localIp;
    private readonly TrackerProxyOptions proxy;
    private readonly ObservableCollection<DiagnosticRow> rows = [];

    public NetworkDiagnosticsWindow()
        : this([], string.Empty, new TrackerProxyOptions(TrackerProxyMode.None))
    {
    }

    public NetworkDiagnosticsWindow(
        IReadOnlyList<string> trackers,
        string localIp,
        TrackerProxyOptions proxy)
    {
        InitializeComponent();
        this.trackers = trackers;
        this.localIp = localIp;
        this.proxy = proxy;
        ResultsList.ItemsSource = rows;
        Opened += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        var service = new NetworkDiagnosticsService();
        int failures = 0;
        foreach (string tracker in trackers)
        {
            rows.Add(new DiagnosticRow("Tracker", "Info", SensitiveDataRedactor.RedactUrl(tracker), string.Empty));
            IReadOnlyList<NetworkDiagnosticResult> results = await service.RunAsync(tracker, localIp, proxy);
            foreach (NetworkDiagnosticResult result in results)
            {
                failures += result.Status == NetworkDiagnosticStatus.Failed ? 1 : 0;
                rows.Add(new DiagnosticRow(
                    result.Test,
                    result.Status.ToString(),
                    result.Details,
                    result.ElapsedMilliseconds > 0 ? $"{result.ElapsedMilliseconds} ms" : string.Empty));
            }
        }

        SummaryText.Text = trackers.Count == 0
            ? "Open a torrent before running network diagnostics."
            : failures == 0
                ? $"{trackers.Count} tracker(s) checked successfully."
                : $"Checks completed with {failures} failure(s).";
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

}

public sealed record DiagnosticRow(string Test, string Status, string Details, string Latency);
