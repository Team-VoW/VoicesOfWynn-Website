namespace VoW.Api.Domain.Reports;

public sealed record ReportSearchCriteria(
    string? Npc,
    string? Content,
    string? Status,
    ReportSortField? SortBy,
    SortDirection? SortDir,
    int Page,
    int PageSize);
