namespace VoW.Api.Domain.Reports;

public sealed record ReportSearchCriteria(
    string? Npc,
    string? Content,
    string? Status,
    int Page,
    int PageSize);
