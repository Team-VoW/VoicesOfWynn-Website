using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(int reportId, string status, CancellationToken cancellationToken);

    /// <summary>
    /// Records a report of an unvoiced line, or folds it into the existing report for the same
    /// chat message - averaging the coordinates and bumping the counter.
    /// </summary>
    Task<LineReportOutcome> CreateOrIncrementAsync(NewLineReport report, CancellationToken cancellationToken);

    /// <summary>Lines matching a status set, for the Discord bot's line listings.</summary>
    Task<LineQueryPage> QueryLinesAsync(LineQueryCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// Sets every supplied chat message to <paramref name="status"/>, inserting a report row for
    /// any that does not exist yet. Idempotent: re-running with the same input converges to the
    /// same state.
    /// </summary>
    Task<LineStatusUpsertCounts> UpsertLineStatusAsync(
        IReadOnlyList<string> chatMessages,
        string status,
        int chunkSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets every supplied chat message to status 'fixed', inserting a report row for any that does
    /// not exist yet. Idempotent: re-running with the same input converges to the same state.
    /// </summary>
    Task<VoicedLineImportCounts> MarkLinesAsVoicedAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken);

    Task<int> DeleteLinesAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken);

    /// <summary>
    /// The most recently reported position for each of the given NPC names, skipping reports that
    /// carry no coordinates.
    /// </summary>
    /// <param name="npcNames">
    /// Degenerated NPC names - lowercase, alphanumeric only. That is the form the mod reports and
    /// therefore the only form report.npc_name ever holds; matching display names here resolves
    /// nothing for any NPC whose name has a space in it.
    /// </param>
    Task<IReadOnlyDictionary<string, ReportPosition>> GetLastPositionsByNpcNameAsync(
        IReadOnlyList<string> npcNames,
        CancellationToken cancellationToken);
}
