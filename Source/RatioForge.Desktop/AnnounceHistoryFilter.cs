namespace RatioForge.Desktop;

/// <summary>Selects announce history rows by event, protocol, and outcome; empty criteria match everything.</summary>
public sealed record AnnounceHistoryFilter(string Event = "", string Protocol = "", string Status = "")
{
    public IEnumerable<AnnounceHistoryRow> Apply(IEnumerable<AnnounceHistoryRow> entries) => entries.Where(Matches);

    public bool Matches(AnnounceHistoryRow entry) =>
        (string.IsNullOrEmpty(Event) || entry.Event.Equals(Event, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrEmpty(Protocol) || entry.Protocol.Equals(Protocol, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrEmpty(Status) ||
         (Status == "Success" && !entry.Status.Equals("Error", StringComparison.OrdinalIgnoreCase)) ||
         entry.Status.Equals(Status, StringComparison.OrdinalIgnoreCase));
}
