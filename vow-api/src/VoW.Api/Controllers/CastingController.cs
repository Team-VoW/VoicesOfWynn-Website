using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Casting;

namespace VoW.Api.Controllers;

/// <summary>
/// Staff voting on open casting rounds. Responses only ever describe the caller's own votes; other
/// voters appear as anonymous comment text once the caller has marked the character done.
/// </summary>
[ApiController]
[RequireCapability(Capability.CastingVote)]
[Route("casting")]
[RequestSizeLimit(16384)]
public sealed class CastingController(ICastingVotingService votingService) : ControllerBase
{
    [HttpGet("rounds")]
    public async Task<ActionResult<CastingRoundListResponse>> GetRounds(CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? Ok(await votingService.GetOpenRoundsAsync(userId, cancellationToken))
            : Unauthorized();

    [HttpGet("rounds/{roundId:int}")]
    public async Task<ActionResult<CastingRoundDetailResponse>> GetRound(int roundId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var round = await votingService.GetRoundAsync(roundId, userId, cancellationToken);
        return round is null ? NotFound() : Ok(round);
    }

    [HttpGet("rounds/{roundId:int}/my-votes")]
    public async Task<ActionResult<CastingMyPicksResponse>> GetMyVotes(int roundId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var picks = await votingService.GetMyPicksAsync(roundId, userId, cancellationToken);
        return picks is null ? NotFound() : Ok(picks);
    }

    [HttpGet("characters/{characterId:int}/auditions")]
    public async Task<ActionResult<CastingAuditionListResponse>> GetAuditions(int characterId, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var auditions = await votingService.GetAuditionsAsync(characterId, userId, cancellationToken);
        return auditions is null ? NotFound() : Ok(auditions);
    }

    [HttpPut("auditions/{auditionId:int}/vote")]
    public async Task<IActionResult> Vote(int auditionId, [FromBody] CastVoteRequest request, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.VoteAsync(auditionId, userId, request.Comment, cancellationToken))
            : Unauthorized();

    [HttpDelete("auditions/{auditionId:int}/vote")]
    public async Task<IActionResult> RemoveVote(int auditionId, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.RemoveVoteAsync(auditionId, userId, cancellationToken))
            : Unauthorized();

    [HttpPut("auditions/{auditionId:int}/comment")]
    public async Task<IActionResult> SetComment(int auditionId, [FromBody] SetCommentRequest request, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.SetCommentAsync(auditionId, userId, request.Comment, cancellationToken))
            : Unauthorized();

    [HttpDelete("auditions/{auditionId:int}/comment")]
    public async Task<IActionResult> DeleteComment(int auditionId, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.DeleteCommentAsync(auditionId, userId, cancellationToken))
            : Unauthorized();

    [HttpDelete("characters/{characterId:int}/votes")]
    public async Task<IActionResult> ClearVotes(int characterId, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.ClearVotesAsync(characterId, userId, cancellationToken))
            : Unauthorized();

    [HttpPut("characters/{characterId:int}/done")]
    public async Task<IActionResult> MarkDone(int characterId, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.SetDoneAsync(characterId, userId, true, cancellationToken))
            : Unauthorized();

    [HttpDelete("characters/{characterId:int}/done")]
    public async Task<IActionResult> Reopen(int characterId, CancellationToken cancellationToken) =>
        User.GetUserId() is { } userId
            ? ToActionResult(await votingService.SetDoneAsync(characterId, userId, false, cancellationToken))
            : Unauthorized();

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
