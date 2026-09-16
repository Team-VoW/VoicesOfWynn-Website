using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Reports;
using VoW.Api.Services.Reports;

namespace VoW.Api.Controllers;

/// <summary>
/// Report ingestion from the mod client. Replaces the PHP /api/unvoiced-line-report/new, which
/// was unauthenticated, unthrottled, and crashed on any missing form field.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("reports")]
[RequestSizeLimit(4096)]
public sealed class ReportsController(ILineReportService lineReportService) : ControllerBase
{
    /// <remarks>Forwarded headers are honoured (see Program.cs), so this is the player's address.</remarks>
    private string ConnectionIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost]
    public async Task<ActionResult<SubmitLineReportResponse>> Submit(
        [FromBody] SubmitLineReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lineReportService.SubmitAsync(request, ConnectionIp, cancellationToken);

        if (result.IsRateLimited)
        {
            // Budgets reset on the hour, so the wait is the remainder of the current one.
            var now = DateTime.UtcNow;
            var seconds = 3600 - ((now.Minute * 60) + now.Second);
            Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = "Too many requests. Retry after the indicated delay.", retryAfterSeconds = seconds });
        }

        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return result.Created
            ? StatusCode(StatusCodes.Status201Created, result.Response)
            : Ok(result.Response);
    }
}
