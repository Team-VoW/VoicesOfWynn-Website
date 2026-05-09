using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Reports;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Reports;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.ReportsView)]
[Route("admin/reports")]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<ReportSearchResponse>> Search(
        [FromQuery] ReportSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reportService.SearchAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            foreach (var (field, message) in result.Errors)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    [HttpPatch("{reportId:int}/status")]
    [RequireCapability(Capability.ReportsManage)]
    public async Task<IActionResult> UpdateStatus(
        int reportId,
        [FromBody] UpdateReportStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reportService.UpdateStatusAsync(reportId, request.Status, cancellationToken);
        if (result.Errors.Count > 0)
        {
            foreach (var (field, message) in result.Errors)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return result.Found ? NoContent() : NotFound();
    }

    [HttpDelete("{reportId:int}")]
    [RequireCapability(Capability.ReportsManage)]
    public async Task<IActionResult> Delete(int reportId, CancellationToken cancellationToken)
    {
        var result = await reportService.DeleteAsync(reportId, cancellationToken);
        return result.Found ? NoContent() : NotFound();
    }
}
