using Microsoft.AspNetCore.Mvc;
using VoW.Api.Controllers;
using VoW.Api.Contracts.Reports;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Reports;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.ReportsView)]
[Route("admin/reports")]
public sealed class ReportsController(
    IReportService reportService,
    IReportImportService reportImportService) : ControllerBase
{
    /// <summary>
    /// sounds.json is ~2.5 MB today and grows with every release, so this leaves plenty of headroom
    /// while staying under Kestrel's default 30 MB request body limit.
    /// </summary>
    private const int SoundsJsonMaxSizeBytes = 16_000_000;

    [HttpGet("search")]
    public async Task<ActionResult<ReportSearchResponse>> Search(
        [FromQuery] ReportSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await reportService.SearchAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
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
            ModelState.AddErrors(result.Errors);
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

    [HttpPost("import/voiced")]
    [RequireCapability(Capability.ReportsManage)]
    [RequestSizeLimit(SoundsJsonMaxSizeBytes)]
    public async Task<ActionResult<ImportVoicedLinesResponse>> ImportVoicedLines(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "A sounds.json file is required.");
            return ValidationProblem(ModelState);
        }

        if (file.Length > SoundsJsonMaxSizeBytes)
        {
            ModelState.AddModelError(nameof(file), $"The file must not exceed {SoundsJsonMaxSizeBytes} bytes.");
            return ValidationProblem(ModelState);
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".json", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(file), "Only .json files are accepted.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        var result = await reportImportService.ImportVoicedLinesAsync(stream, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }
}
