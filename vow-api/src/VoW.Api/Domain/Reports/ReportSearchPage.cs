namespace VoW.Api.Domain.Reports;

public sealed record ReportSearchPage(
    int Total,
    int Page,
    IReadOnlyList<ReportSummary> Results);
