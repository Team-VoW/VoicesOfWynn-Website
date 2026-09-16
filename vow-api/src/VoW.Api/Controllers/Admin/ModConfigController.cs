using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Mod;
using VoW.Api.Controllers;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Mod;

namespace VoW.Api.Controllers.Admin;

/// <summary>
/// Editing of what the mod is told on bootup: the current release, timed broadcasts and the fun
/// fact library. Restricted to Project Director and Admin.
/// </summary>
[ApiController]
[RequireCapability(Capability.SystemAdmin)]
[Route("admin/mod")]
public sealed class ModConfigController(IModConfigService modConfigService) : ControllerBase
{
    [HttpGet("release")]
    public async Task<ActionResult<ModReleaseResponse>> GetRelease(CancellationToken cancellationToken)
    {
        var release = await modConfigService.GetReleaseAsync(cancellationToken);
        return release is null ? NotFound() : Ok(release);
    }

    [HttpPut("release")]
    public async Task<IActionResult> UpdateRelease(
        [FromBody] UpdateModReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await modConfigService.UpdateReleaseAsync(request, User.GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("broadcasts")]
    public async Task<ActionResult<BroadcastListResponse>> GetBroadcasts(CancellationToken cancellationToken) =>
        Ok(await modConfigService.GetBroadcastsAsync(cancellationToken));

    [HttpPost("broadcasts")]
    public async Task<IActionResult> CreateBroadcast(
        [FromBody] SaveBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var (result, id) = await modConfigService.CreateBroadcastAsync(request, User.GetUserId(), cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id })
            : ToActionResult(result);
    }

    [HttpPut("broadcasts/{broadcastId:int}")]
    public async Task<IActionResult> UpdateBroadcast(
        int broadcastId,
        [FromBody] SaveBroadcastRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await modConfigService.UpdateBroadcastAsync(broadcastId, request, cancellationToken));

    [HttpDelete("broadcasts/{broadcastId:int}")]
    public async Task<IActionResult> DeleteBroadcast(int broadcastId, CancellationToken cancellationToken) =>
        ToActionResult(await modConfigService.DeleteBroadcastAsync(broadcastId, cancellationToken));

    [HttpGet("fun-facts")]
    public async Task<ActionResult<FunFactListResponse>> GetFunFacts(CancellationToken cancellationToken) =>
        Ok(await modConfigService.GetFunFactsAsync(cancellationToken));

    [HttpPost("fun-facts")]
    public async Task<IActionResult> CreateFunFact(
        [FromBody] SaveFunFactRequest request,
        CancellationToken cancellationToken)
    {
        var (result, id) = await modConfigService.CreateFunFactAsync(request, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id })
            : ToActionResult(result);
    }

    [HttpPut("fun-facts/{funFactId:int}")]
    public async Task<IActionResult> UpdateFunFact(
        int funFactId,
        [FromBody] SaveFunFactRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await modConfigService.UpdateFunFactAsync(funFactId, request, cancellationToken));

    [HttpDelete("fun-facts/{funFactId:int}")]
    public async Task<IActionResult> DeleteFunFact(int funFactId, CancellationToken cancellationToken) =>
        ToActionResult(await modConfigService.DeleteFunFactAsync(funFactId, cancellationToken));

    private IActionResult ToActionResult(ModConfigMutationResult result)
    {
        if (!result.Found)
        {
            return NotFound();
        }

        if (result.Errors.Count > 0)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return NoContent();
    }
}
