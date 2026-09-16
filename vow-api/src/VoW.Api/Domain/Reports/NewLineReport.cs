namespace VoW.Api.Domain.Reports;

public sealed record NewLineReport(
    string ChatMessage,
    string? NpcName,
    string PlayerName,
    ReportPosition? Position);
