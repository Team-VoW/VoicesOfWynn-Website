namespace VoW.Api.Contracts.Reports;

/// <summary>
/// Outcome of importing a sounds.json file and marking its lines as voiced.
/// <see cref="AlreadyPresent"/> counts lines that already had a report row and were forced to
/// 'fixed' - it is deliberately not called "updated", because it does not mean "rows whose value
/// changed" (a line already at 'fixed' still counts here).
/// </summary>
public sealed record ImportVoicedLinesResponse(
    int TotalEntries,
    int UniqueLines,
    int AlreadyPresent,
    int RowsInserted,
    ImportVoicedLinesSkipped Skipped);

/// <summary>
/// Entries that were not imported, by reason. The reasons are evaluated in declaration order and
/// are mutually exclusive, so TotalEntries == UniqueLines + Skipped.Total.
/// </summary>
public sealed record ImportVoicedLinesSkipped(
    int NoAudioFile,
    int BlankLine,
    int TooLong,
    int DuplicateInFile,
    int Total);
