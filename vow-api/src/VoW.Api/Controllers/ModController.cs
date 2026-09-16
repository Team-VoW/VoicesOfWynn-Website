using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Mod;
using VoW.Api.Services.Mod;

namespace VoW.Api.Controllers;

/// <summary>
/// The mod client's own endpoints. Replaces the PHP /api/version/check, which was a GET that
/// wrote two rows per call and whose response body was whatever keys version.ini happened to have.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("mod")]
[RequestSizeLimit(2048)]
public sealed class ModController(IModBootupService bootupService) : ControllerBase
{
    /// <remarks>
    /// Forwarded headers are honoured (see Program.cs), so behind the reverse proxy this is the
    /// player's address rather than the proxy's - the per-IP bootup throttle depends on it.
    /// </remarks>
    private string ConnectionIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPost("bootup")]
    public async Task<ActionResult<ModBootupResponse>> Bootup(
        [FromBody] ModBootupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bootupService.BootupAsync(request, ConnectionIp, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        // Carries a randomly chosen fun fact and drives a kill switch; must never be cached.
        Response.Headers.CacheControl = "no-store";
        return Ok(result.Response);
    }
}
