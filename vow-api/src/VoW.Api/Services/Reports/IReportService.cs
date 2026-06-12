using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public interface IReportService
{
    Task<ReportSearchServiceResult> SearchAsync(ReportSearchRequest request, CancellationToken cancellationToken);

    Task<ReportMutationResult> UpdateStatusAsync(int reportId, string status, CancellationToken cancellationToken);

    Task<ReportMutationResult> DeleteAsync(int reportId, CancellationToken cancellationToken);
}
