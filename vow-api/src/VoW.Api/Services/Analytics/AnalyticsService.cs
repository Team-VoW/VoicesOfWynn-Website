using VoW.Api.Contracts.Analytics;
using VoW.Api.Domain.Analytics;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Analytics;

public sealed class AnalyticsService(IAnalyticsRepository analyticsRepository) : IAnalyticsService
{
    private const int MaxDaysPerRun = 400;

    public async Task<DailyUsageServiceResult> GetDailyUsageAsync(
        DailyUsageRequest request,
        CancellationToken cancellationToken)
    {
        var range = string.IsNullOrWhiteSpace(request.Range)
            ? DailyUsageRange.Last90Days
            : request.Range.Trim().ToLowerInvariant();

        if (!DailyUsageRange.IsValid(range))
        {
            return DailyUsageServiceResult.Failure(nameof(request.Range), "Range must be one of 30, 90, 365, or all.");
        }

        var days = DailyUsageRange.ToDays(range);
        var rows = await analyticsRepository.GetDailyUsageAsync(days, cancellationToken);
        var summary = await BuildSummaryAsync(range, days, rows, cancellationToken);

        return DailyUsageServiceResult.Success(new DailyUsageResponse(
            summary.Range,
            summary.TotalBootups,
            summary.AverageBootupsPerDay,
            ToNullableResponse(summary.PeakDay),
            summary.PreviousPeriodChangePercent,
            summary.Points.Select(ToResponse).ToList()));
    }

    /// <remarks>
    /// Days are only ever aggregated once the bootup throttles guarantee no further pings can
    /// land on them, and at most <see cref="MaxDaysPerRun"/> are handled per call so a job that
    /// has not run in a long time cannot tie the request up indefinitely.
    /// </remarks>
    public async Task<AggregateUsageResponse> AggregateAsync(CancellationToken cancellationToken)
    {
        var throughDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-BootupThrottle.AggregationLagDays);
        var result = await analyticsRepository.AggregateAsync(throughDate, MaxDaysPerRun, cancellationToken);
        return new AggregateUsageResponse(result.DaysProcessed, result.BootupsAggregated, result.ThroughDate);
    }

    private async Task<DailyUsageSummary> BuildSummaryAsync(
        string range,
        int? days,
        IReadOnlyList<DailyUsageRow> rows,
        CancellationToken cancellationToken)
    {
        var points = BuildPoints(rows);
        var totalBootups = rows.Sum(row => row.Bootups);
        var averageBootupsPerDay = rows.Count == 0 ? 0 : Math.Round((decimal)totalBootups / rows.Count, 2);
        var peakDay = points.OrderByDescending(point => point.Bootups).ThenBy(point => point.Date).FirstOrDefault();
        var previousPeriodChangePercent = days is null
            ? null
            : await CalculatePreviousPeriodChangeAsync(days.Value, totalBootups, cancellationToken);

        return new DailyUsageSummary(
            range,
            totalBootups,
            averageBootupsPerDay,
            peakDay,
            previousPeriodChangePercent,
            points);
    }

    private async Task<decimal?> CalculatePreviousPeriodChangeAsync(
        int days,
        int currentBootups,
        CancellationToken cancellationToken)
    {
        var previousBootups = await analyticsRepository.GetPreviousPeriodBootupsAsync(days, cancellationToken);
        if (previousBootups is null or 0)
        {
            return null;
        }

        return Math.Round(((decimal)currentBootups - previousBootups.Value) / previousBootups.Value * 100, 2);
    }

    private static IReadOnlyList<DailyUsagePoint> BuildPoints(IReadOnlyList<DailyUsageRow> rows)
    {
        var points = new List<DailyUsagePoint>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var windowStart = Math.Max(0, i - 6);
            var window = rows.Skip(windowStart).Take(i - windowStart + 1).ToArray();
            var rollingAverage = Math.Round((decimal)window.Sum(row => row.Bootups) / window.Length, 2);
            points.Add(new DailyUsagePoint(rows[i].Date, rows[i].Bootups, rollingAverage));
        }

        return points;
    }

    private static DailyUsagePointResponse? ToNullableResponse(DailyUsagePoint? point) =>
        point is null ? null : ToResponse(point);

    private static DailyUsagePointResponse ToResponse(DailyUsagePoint point) =>
        new(point.Date, point.Bootups, point.RollingAverage7Day);
}
