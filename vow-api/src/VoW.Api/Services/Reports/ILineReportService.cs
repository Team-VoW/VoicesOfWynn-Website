using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public interface ILineReportService
{
    /// <summary>Records a mod client's report of an unvoiced line.</summary>
    Task<LineReportSubmissionResult> SubmitAsync(
        SubmitLineReportRequest request,
        string callerIp,
        CancellationToken cancellationToken);

    Task<LineQueryServiceResult> QueryAsync(LineQueryRequest request, CancellationToken cancellationToken);

    Task<LineStatusServiceResult> SetStatusAsync(SetLineStatusRequest request, CancellationToken cancellationToken);

    Task<DeleteLinesResponse> DeleteAsync(DeleteLinesRequest request, CancellationToken cancellationToken);
}
