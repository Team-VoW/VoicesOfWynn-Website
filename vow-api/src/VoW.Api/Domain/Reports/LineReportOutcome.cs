namespace VoW.Api.Domain.Reports;

/// <param name="Created">True when this was the first report of the line.</param>
/// <param name="ReportedTimes">How many times the line has now been reported.</param>
public sealed record LineReportOutcome(bool Created, int ReportedTimes);
