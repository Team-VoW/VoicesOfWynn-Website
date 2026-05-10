namespace VoW.Api.Domain.Analytics;

public sealed record DailyUsagePoint(
    DateOnly Date,
    int Bootups,
    decimal RollingAverage7Day);
