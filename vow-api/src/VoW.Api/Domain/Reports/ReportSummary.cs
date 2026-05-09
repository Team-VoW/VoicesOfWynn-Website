namespace VoW.Api.Domain.Reports;

public sealed record ReportSummary(
    int ReportId,
    string? NpcName,
    string ChatMessage,
    string Status,
    int ReportedTimes,
    DateTime TimeSubmitted);
