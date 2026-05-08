using VoW.Api.Models;

namespace VoW.Api.Repositories;

public interface IReportRepository
{
    Task<ReportSearchResponse> SearchAsync(ReportSearchRequest request, CancellationToken cancellationToken);
}
