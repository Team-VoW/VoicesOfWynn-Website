using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Reports;
using VoW.Api.Controllers;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Reports;

namespace VoW.Api.Controllers.Bot;

/// <summary>
/// The Discord bot's view of unvoiced line reports. Replaces the PHP accepted / active / valid /
/// import routes, which authenticated with a shared key passed in the query string.
/// </summary>
[ApiController]
[RequireDiscordBotApiKey]
[Route("bot/reports")]
public sealed class ReportsController(ILineReportService lineReportService) : ControllerBase
{
    [HttpGet("lines")]
    public async Task<ActionResult<LineQueryResponse>> Lines(
        [FromQuery] LineQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lineReportService.QueryAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    [HttpPost("lines/status")]
    public async Task<ActionResult<SetLineStatusResponse>> SetStatus(
        [FromBody] SetLineStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lineReportService.SetStatusAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    [HttpDelete("lines")]
    public async Task<ActionResult<DeleteLinesResponse>> Delete(
        [FromBody] DeleteLinesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await lineReportService.DeleteAsync(request, cancellationToken));
}
