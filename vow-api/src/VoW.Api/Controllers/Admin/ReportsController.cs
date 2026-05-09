using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Models;
using VoW.Api.Repositories;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/reports")]
public sealed class ReportsController(IReportRepository reportRepository) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<ReportSearchResponse>> Search(
        [FromQuery] ReportSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Status) && !ReportStatus.IsValid(request.Status))
        {
            ModelState.AddModelError(nameof(request.Status), $"Status must be one of {ReportStatus.DisplayList}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await reportRepository.SearchAsync(request, cancellationToken));
    }
}
