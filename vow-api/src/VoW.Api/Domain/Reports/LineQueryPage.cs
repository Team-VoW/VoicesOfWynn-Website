namespace VoW.Api.Domain.Reports;

public sealed record LineQueryPage(
    int Total,
    IReadOnlyList<LineReportLine> Results);
