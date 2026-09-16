namespace VoW.Api.Contracts.Analytics;

public sealed record AggregateUsageResponse(
    int DaysProcessed,
    int BootupsAggregated,
    DateOnly? ThroughDate);
