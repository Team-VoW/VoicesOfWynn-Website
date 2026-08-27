namespace VoW.Api.Domain.Reports;

/// <param name="AlreadyPresent">Lines that already had a report row before the import.</param>
/// <param name="Inserted">Report rows created by the import.</param>
public sealed record VoicedLineImportCounts(int AlreadyPresent, int Inserted);
