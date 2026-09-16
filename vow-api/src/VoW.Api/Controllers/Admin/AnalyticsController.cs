using Microsoft.AspNetCore.Mvc;
using VoW.Api.Controllers;
using VoW.Api.Contracts.Analytics;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Analytics;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[RequireCapability(Capability.AnalyticsView)]
[Route("admin/analytics")]
public sealed class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("daily")]
    public async Task<ActionResult<DailyUsageResponse>> Daily(
        [FromQuery] DailyUsageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await analyticsService.GetDailyUsageAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// Rolls raw bootup pings into the aggregated daily table that <see cref="Daily"/> reads.
    /// Safe to run repeatedly: days already aggregated are skipped.
    /// </summary>
    [HttpPost("aggregate")]
    [RequireCapability(Capability.SystemAdmin)]
    public async Task<ActionResult<AggregateUsageResponse>> Aggregate(CancellationToken cancellationToken) =>
        Ok(await analyticsService.AggregateAsync(cancellationToken));
}
