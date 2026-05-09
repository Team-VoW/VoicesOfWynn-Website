using VoW.Api.Contracts.Reports;
using VoW.Api.Domain.Reports;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Reports;

public sealed class ReportService(IReportRepository reportRepository) : IReportService
{
    public async Task<ReportSearchServiceResult> SearchAsync(
        ReportSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Status) && !ReportStatus.IsValid(request.Status))
        {
            return ReportSearchServiceResult.Failure(
                nameof(request.Status),
                $"Status must be one of {ReportStatus.DisplayList}.");
        }

        var criteria = new ReportSearchCriteria(
            NormalizeFilter(request.Npc),
            NormalizeFilter(request.Content),
            string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.ToLowerInvariant(),
            request.SortBy,
            request.SortDir,
            request.Page,
            request.PageSize);

        var page = await reportRepository.SearchAsync(criteria, cancellationToken);
        return ReportSearchServiceResult.Success(new ReportSearchResponse(
            page.Total,
            page.Page,
            page.Results.Select(result => new ReportSearchResult(
                result.ReportId,
                result.NpcName,
                result.ChatMessage,
                result.Status,
                result.ReportedTimes,
                result.TimeSubmitted)).ToList()));
    }

    public async Task<ReportMutationResult> UpdateStatusAsync(
        int reportId,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status) || !ReportStatus.IsValid(status))
        {
            return ReportMutationResult.Invalid(
                nameof(status),
                $"Status must be one of {ReportStatus.DisplayList}.");
        }

        var found = await reportRepository.UpdateStatusAsync(reportId, status.ToLowerInvariant(), cancellationToken);
        return found ? ReportMutationResult.Success() : ReportMutationResult.NotFound();
    }

    public async Task<ReportMutationResult> DeleteAsync(int reportId, CancellationToken cancellationToken)
    {
        var found = await reportRepository.DeleteAsync(reportId, cancellationToken);
        return found ? ReportMutationResult.Success() : ReportMutationResult.NotFound();
    }

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
