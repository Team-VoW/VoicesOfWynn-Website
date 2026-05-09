using VoW.Api.Contracts.Reports;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchResponse> SearchAsync(ReportSearchRequest request, CancellationToken cancellationToken);
}
