namespace VoW.Api.Domain.Reports;

/// <summary>
/// Replaces the three near-identical PHP endpoints (accepted / active / valid) that differed only
/// in which statuses they selected.
/// </summary>
public sealed record LineQueryCriteria(
    string? Npc,
    IReadOnlyList<string> Statuses,
    int MinReports,
    DateOnly? Since,
    int Limit,
    int Offset);
