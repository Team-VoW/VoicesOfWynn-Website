namespace VoW.Api.Contracts.Reports;

public sealed record ReportSearchResponse(
    int Total,
    int Page,
    IReadOnlyList<ReportSearchResult> Results);
