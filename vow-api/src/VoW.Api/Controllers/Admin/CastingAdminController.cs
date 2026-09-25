using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Casting;
using VoW.Api.Controllers;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Casting;

namespace VoW.Api.Controllers.Admin;

/// <summary>
/// Running casting rounds: the line-up of characters and auditions, the round lifecycle, CCC imports,
/// and the named review of who voted for what. Admin, Project Director and Cast Manager.
/// </summary>
[ApiController]
[RequireCapability(Capability.CastingManage)]
[Route("admin/casting")]
public sealed class CastingAdminController(
    ICastingAdminService adminService,
    ICastingReviewService reviewService) : ControllerBase
{
    private const long AuditionMaxSizeBytes = 50L * 1024 * 1024;

    [HttpGet("rounds")]
    public async Task<ActionResult<AdminCastingRoundListResponse>> GetRounds(
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await adminService.GetRoundsAsync(includeArchived, cancellationToken));

    [HttpGet("rounds/{roundId:int}")]
    public async Task<ActionResult<AdminCastingRoundDetailResponse>> GetRound(int roundId, CancellationToken cancellationToken)
    {
        var round = await adminService.GetRoundAsync(roundId, cancellationToken);
        return round is null ? NotFound() : Ok(round);
    }

    [HttpPost("rounds")]
    public async Task<IActionResult> CreateRound([FromBody] SaveCastingRoundRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.CreateRoundAsync(request, User.GetUserId(), cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : ToActionResult(result.Result);
    }

    [HttpPut("rounds/{roundId:int}")]
    public async Task<IActionResult> UpdateRound(
        int roundId,
        [FromBody] SaveCastingRoundRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.UpdateRoundAsync(roundId, request, cancellationToken));

    [HttpPost("rounds/{roundId:int}/status")]
    public async Task<IActionResult> SetStatus(
        int roundId,
        [FromBody] SetCastingRoundStatusRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.SetStatusAsync(roundId, request.Status!.Value, cancellationToken));

    [HttpDelete("rounds/{roundId:int}")]
    public async Task<IActionResult> DeleteRound(int roundId, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.DeleteRoundAsync(roundId, cancellationToken));

    [HttpPost("rounds/{roundId:int}/import/ccc")]
    public async Task<IActionResult> ImportCcc(int roundId, [FromBody] ImportCccRequest request, CancellationToken cancellationToken)
    {
        var result = await adminService.StartCccImportAsync(roundId, request.Url, cancellationToken);
        return result.Succeeded ? Accepted(new { queued = true }) : ToActionResult(result);
    }

    [HttpGet("rounds/{roundId:int}/review")]
    public async Task<ActionResult<CastingReviewResponse>> GetReview(int roundId, CancellationToken cancellationToken)
    {
        var review = await reviewService.GetReviewAsync(roundId, cancellationToken);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPost("rounds/{roundId:int}/characters")]
    public async Task<IActionResult> CreateCharacter(
        int roundId,
        [FromBody] SaveCastingCharacterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await adminService.CreateCharacterAsync(roundId, request, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : ToActionResult(result.Result);
    }

    [HttpPut("characters/{characterId:int}")]
    public async Task<IActionResult> UpdateCharacter(
        int characterId,
        [FromBody] SaveCastingCharacterRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.UpdateCharacterAsync(characterId, request, cancellationToken));

    [HttpDelete("characters/{characterId:int}")]
    public async Task<IActionResult> DeleteCharacter(int characterId, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.DeleteCharacterAsync(characterId, cancellationToken));

    [HttpPut("characters/{characterId:int}/winner")]
    public async Task<IActionResult> SetWinner(
        int characterId,
        [FromBody] SetCastingWinnerRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.SetWinnerAsync(characterId, request.AuditionId, cancellationToken));

    [HttpPost("characters/{characterId:int}/auditions")]
    [RequestSizeLimit(AuditionMaxSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AuditionMaxSizeBytes)]
    public async Task<IActionResult> UploadAudition(
        int characterId,
        [FromForm] UploadCastingAuditionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            ModelState.AddModelError(nameof(request.File), "An audio file is required.");
            return ValidationProblem(ModelState);
        }

        await using var stream = request.File.OpenReadStream();
        var result = await adminService.UploadAuditionAsync(characterId, request.AuditioneeName, stream, cancellationToken);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { id = result.Value })
            : ToActionResult(result.Result);
    }

    [HttpDelete("auditions/{auditionId:int}")]
    public async Task<IActionResult> DeleteAudition(int auditionId, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.DeleteAuditionAsync(auditionId, cancellationToken));

    private IActionResult ToActionResult(CastingResult result)
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
