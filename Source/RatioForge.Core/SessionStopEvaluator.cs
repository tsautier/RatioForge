namespace RatioForge;

/// <summary>Evaluates automatic session-stop conditions using elapsed time and byte counters.</summary>
public static class SessionStopEvaluator
{
    private const decimal BytesPerMebibyte = 1024 * 1024;

    public static bool ShouldStop(
        SessionStopCondition condition,
        decimal value,
        TimeSpan elapsed,
        long uploaded,
        long downloaded) => condition switch
        {
            SessionStopCondition.AfterDuration => elapsed.TotalSeconds >= (double)value,
            SessionStopCondition.Uploaded => uploaded >= value * BytesPerMebibyte,
            SessionStopCondition.Downloaded => downloaded >= value * BytesPerMebibyte,
            SessionStopCondition.Ratio => downloaded > 0 && uploaded / (decimal)downloaded >= value,
            _ => false,
        };
}
