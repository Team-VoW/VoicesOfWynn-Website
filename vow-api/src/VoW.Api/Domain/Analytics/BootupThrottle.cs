namespace VoW.Api.Domain.Analytics;

/// <summary>
/// How long must pass before another bootup from the same client counts. Carried over verbatim
/// from the PHP BootupLogger so the daily numbers stay comparable across the migration.
/// </summary>
public static class BootupThrottle
{
    public static readonly TimeSpan ByIp = TimeSpan.FromSeconds(3600);

    public static readonly TimeSpan ByClientId = TimeSpan.FromSeconds(86400);

    /// <summary>
    /// A day is only aggregated once no further pings for it can arrive - the longest throttle
    /// window, rounded up to whole days, plus one for the day currently in progress.
    /// </summary>
    public static int AggregationLagDays =>
        (int)Math.Ceiling(Math.Max(ByIp.TotalSeconds, ByClientId.TotalSeconds) / 86400d) + 1;
}
