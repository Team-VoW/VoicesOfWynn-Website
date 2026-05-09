using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken cancellationToken);
}
