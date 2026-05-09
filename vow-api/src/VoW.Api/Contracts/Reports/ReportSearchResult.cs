namespace VoW.Api.Contracts.Reports;

public sealed record ReportSearchResult(
    int ReportId,
    string? NpcName,
    string ChatMessage,
    string Status,
    int ReportedTimes,
    DateTime TimeSubmitted);
