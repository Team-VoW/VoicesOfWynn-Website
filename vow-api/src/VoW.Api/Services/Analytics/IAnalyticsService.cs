using VoW.Api.Contracts.Analytics;

namespace VoW.Api.Services.Analytics;

public interface IAnalyticsService
{
    Task<DailyUsageServiceResult> GetDailyUsageAsync(
        DailyUsageRequest request,
        CancellationToken cancellationToken);
}
