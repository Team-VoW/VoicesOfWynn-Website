namespace VoW.Api.Contracts.Analytics;

public sealed record DailyUsagePointResponse(
    DateOnly Date,
    int Bootups,
    decimal RollingAverage7Day);
