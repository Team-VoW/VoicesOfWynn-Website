namespace VoW.Api.Domain.Analytics;

public sealed record UsageAggregationResult(
    int DaysProcessed,
    int BootupsAggregated,
    DateOnly? ThroughDate);
