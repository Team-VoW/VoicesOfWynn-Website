using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public interface IReportService
{
    Task<ReportSearchServiceResult> SearchAsync(ReportSearchRequest request, CancellationToken cancellationToken);
}
