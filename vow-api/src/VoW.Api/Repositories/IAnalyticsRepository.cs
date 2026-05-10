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
}
