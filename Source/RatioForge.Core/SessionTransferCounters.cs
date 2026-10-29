namespace RatioForge;

/// <summary>Result of applying a tracker-reported leecher count to the session.</summary>
public enum LeecherStateChange
{
    /// <summary>The count was not reported or did not change.</summary>
    Unchanged,

    /// <summary>The count changed without affecting upload accounting.</summary>
    Updated,

    /// <summary>Upload accounting was paused because the swarm has no leechers.</summary>
    UploadPaused,

    /// <summary>Upload accounting resumed because leechers returned.</summary>
    UploadResumed,
}

/// <summary>Accumulates the simulated transfer counters reported to trackers during a session.</summary>
public sealed class SessionTransferCounters
{
    private DateTimeOffset lastUpdate;

    public long Uploaded { get; private set; }

    public long Downloaded { get; private set; }

    public long InitialCompletedBytes { get; private set; }

    public bool CompletionAnnounced { get; set; }

    public int? LastKnownLeechers { get; private set; }

    public double? Ratio => Downloaded > 0 ? Uploaded / (double)Downloaded : null;

    /// <summary>Clears every counter and the swarm state.</summary>
    public void Reset()
    {
        Uploaded = 0;
        Downloaded = 0;
        InitialCompletedBytes = 0;
        CompletionAnnounced = false;
        LastKnownLeechers = null;
    }

    /// <summary>Prepares a new session; the uploaded total is kept until <see cref="Reset"/> is called.</summary>
    public void Start(long totalSize, decimal completedPercent, DateTimeOffset now)
    {
        InitialCompletedBytes = (long)(totalSize * ((double)completedPercent / 100d));
        Downloaded = 0;
        CompletionAnnounced = InitialCompletedBytes >= totalSize;
        LastKnownLeechers = null;
        lastUpdate = now;
    }

    /// <summary>Adds the transfer accumulated since the previous update.</summary>
    /// <returns><see langword="true"/> when the torrent has just become complete and completion was not announced yet.</returns>
    public bool Advance(
        DateTimeOffset now,
        long? totalSize,
        decimal uploadKibPerSecond,
        decimal downloadKibPerSecond,
        bool pauseUploadWhenNoLeechers)
    {
        bool wasComplete = IsComplete(totalSize);
        double seconds = Math.Max(0, (now - lastUpdate).TotalSeconds);
        lastUpdate = now;
        if (!SessionUploadPolicy.IsPaused(pauseUploadWhenNoLeechers, LastKnownLeechers))
        {
            Uploaded += (long)((double)uploadKibPerSecond * 1024d * seconds);
        }

        Downloaded += (long)((double)downloadKibPerSecond * 1024d * seconds);
        if (totalSize is long size)
        {
            Downloaded = Math.Min(Downloaded, Math.Max(0, size - InitialCompletedBytes));
        }

        return !wasComplete && IsComplete(totalSize) && !CompletionAnnounced;
    }

    public bool IsComplete(long? totalSize) =>
        totalSize is long size && InitialCompletedBytes + Downloaded >= size;

    public double CompletionPercent(long? totalSize) => totalSize is > 0
        ? Math.Clamp((InitialCompletedBytes + Downloaded) * 100d / totalSize.Value, 0, 100)
        : 0;

    public long RemainingBytes(long? totalSize) => totalSize is long size
        ? Math.Max(0, size - InitialCompletedBytes - Downloaded)
        : 0;

    /// <summary>Records the leecher count reported by the tracker.</summary>
    public LeecherStateChange UpdateLeechers(int? leechers, bool pauseUploadWhenNoLeechers)
    {
        if (leechers is null || leechers == LastKnownLeechers)
        {
            return LeecherStateChange.Unchanged;
        }

        bool wasPaused = SessionUploadPolicy.IsPaused(pauseUploadWhenNoLeechers, LastKnownLeechers);
        LastKnownLeechers = leechers;
        bool isPaused = SessionUploadPolicy.IsPaused(pauseUploadWhenNoLeechers, leechers);
        return (wasPaused, isPaused) switch
        {
            (false, true) => LeecherStateChange.UploadPaused,
            (true, false) => LeecherStateChange.UploadResumed,
            _ => LeecherStateChange.Updated,
        };
    }
}
