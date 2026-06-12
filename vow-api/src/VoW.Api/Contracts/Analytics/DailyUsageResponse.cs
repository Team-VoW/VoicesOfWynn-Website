namespace VoW.Api.Contracts.Analytics;

public sealed record DailyUsageResponse(
    string Range,
    int TotalBootups,
    decimal AverageBootupsPerDay,
    DailyUsagePointResponse? PeakDay,
    decimal? PreviousPeriodChangePercent,
    IReadOnlyList<DailyUsagePointResponse> Points);
