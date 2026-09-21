namespace RatioForge;

/// <summary>Determines whether simulated upload accounting is allowed for the reported swarm state.</summary>
public static class SessionUploadPolicy
{
    public static bool IsPaused(bool pauseWhenNoLeechers, int? leechers) =>
        pauseWhenNoLeechers && leechers == 0;
}
