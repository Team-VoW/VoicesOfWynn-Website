using VoW.Api.Contracts.Analytics;

namespace VoW.Api.Services.Analytics;

public interface IAnalyticsService
{
    Task<DailyUsageServiceResult> GetDailyUsageAsync(
        DailyUsageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rolls raw bootup pings into the aggregated daily table, up to the newest day that can no
    /// longer receive pings.
    /// </summary>
    Task<AggregateUsageResponse> AggregateAsync(CancellationToken cancellationToken);
}
