namespace VoW.Api.Domain.Reports;

public sealed record LineReportLine(
    string ChatMessage,
    string? NpcName,
    ReportPosition? Position);
