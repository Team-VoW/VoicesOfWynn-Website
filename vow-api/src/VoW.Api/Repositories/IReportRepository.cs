using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken cancellationToken);

    Task<bool> UpdateStatusAsync(int reportId, string status, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken);
}
