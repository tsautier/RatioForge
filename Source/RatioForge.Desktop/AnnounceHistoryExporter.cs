namespace RatioForge.Desktop;

using System.Text;
using System.Text.Json;

/// <summary>Serializes privacy-filtered announce history for support and analysis.</summary>
public static class AnnounceHistoryExporter
{
    public static string ToCsv(IEnumerable<AnnounceHistoryRow> entries)
    {
        var output = new StringBuilder("Time,Attempt,Event,Tracker,Protocol,Status,Latency,Interval,Result,Request,Diagnostic,FinalUrl\r\n");
        foreach (AnnounceHistoryRow entry in entries)
        {
            output.AppendLine(string.Join(',', new[]
            {
                entry.Time, entry.Attempt.ToString(), entry.Event, SensitiveDataRedactor.RedactUrl(entry.Tracker),
                entry.Protocol, entry.Status, entry.Latency, entry.Interval,
                SensitiveDataRedactor.Redact(entry.Result), SensitiveDataRedactor.RedactUrl(entry.Request),
                SensitiveDataRedactor.Redact(entry.Diagnostic), SensitiveDataRedactor.RedactUrl(entry.FinalUrl),
            }.Select(EscapeCsv)));
        }

        return output.ToString();
    }

    public static string ToJson(IEnumerable<AnnounceHistoryRow> entries) => JsonSerializer.Serialize(
        entries.Select(entry => entry with
        {
            Tracker = SensitiveDataRedactor.RedactUrl(entry.Tracker),
            Result = SensitiveDataRedactor.Redact(entry.Result),
            Request = SensitiveDataRedactor.RedactUrl(entry.Request),
            Diagnostic = SensitiveDataRedactor.Redact(entry.Diagnostic),
            FinalUrl = SensitiveDataRedactor.RedactUrl(entry.FinalUrl),
        }),
        new JsonSerializerOptions { WriteIndented = true });

    private static string EscapeCsv(string value) =>
        '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
