namespace VoW.Api.Domain.Analytics;

public sealed record DailyUsageSummary(
    string Range,
    int TotalBootups,
    decimal AverageBootupsPerDay,
    DailyUsagePoint? PeakDay,
    decimal? PreviousPeriodChangePercent,
    IReadOnlyList<DailyUsagePoint> Points);
