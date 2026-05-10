using Microsoft.AspNetCore.Mvc;
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
            foreach (var (field, message) in result.Errors)
            {
                ModelState.AddModelError(field, message);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(result.Response);
    }
}
