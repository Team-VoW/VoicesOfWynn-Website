namespace VoW.Api.Domain.Reports;

/// <param name="Updated">Lines that already had a report row and were moved to the new status.</param>
/// <param name="Inserted">Report rows created for lines that had never been reported.</param>
public sealed record LineStatusUpsertCounts(int Updated, int Inserted);
