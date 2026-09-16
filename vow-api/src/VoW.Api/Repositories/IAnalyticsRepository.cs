using VoW.Api.Domain.Analytics;

namespace VoW.Api.Repositories;

public interface IAnalyticsRepository
{
    Task<IReadOnlyList<DailyUsageRow>> GetDailyUsageAsync(
        int? days,
        CancellationToken cancellationToken);

    Task<int?> GetPreviousPeriodBootupsAsync(
        int days,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a mod bootup, subject to the per-IP and per-client throttles.
    /// </summary>
    /// <returns>True when a ping row was written, false when the call was throttled.</returns>
    Task<bool> RecordBootupAsync(
        string clientIdHash,
        string ipHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Collapses raw pings into one <c>daily</c> row per day, deleting the pings it counted.
    /// Idempotent: re-running processes only days that have not been rolled up yet.
    /// </summary>
    /// <param name="throughDate">The first day that must NOT be processed.</param>
    /// <param name="maxDays">Upper bound on days handled in one call.</param>
    Task<UsageAggregationResult> AggregateAsync(
        DateOnly throughDate,
        int maxDays,
        CancellationToken cancellationToken);
}
