namespace VoW.Api.Domain.Analytics;

public sealed record DailyUsageRow(
    DateOnly Date,
    int Bootups);
