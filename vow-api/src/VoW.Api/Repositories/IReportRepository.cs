using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(int reportId, string status, CancellationToken cancellationToken);

    /// <summary>
    /// Sets every supplied chat message to status 'fixed', inserting a report row for any that does
    /// not exist yet. Idempotent: re-running with the same input converges to the same state.
    /// </summary>
    Task<VoicedLineImportCounts> MarkLinesAsVoicedAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken);
}
