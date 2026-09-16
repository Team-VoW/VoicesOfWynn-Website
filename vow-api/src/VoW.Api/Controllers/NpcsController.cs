using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Services.Contents;
using VoW.Api.Services.Npcs;

namespace VoW.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("npcs")]
[RequestSizeLimit(16384)]
public sealed class NpcsController(
    INpcPageService pageService,
    INpcRecordingCatalogService recordingService,
    INpcVoteService voteService,
    INpcCommentService commentService) : ControllerBase
{
    /// <remarks>
    /// Forwarded headers are honoured (see Program.cs), so behind the reverse proxy this is the
    /// visitor's address rather than the proxy's.
    /// </remarks>
    private string ConnectionIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <remarks>
    /// The mod-contents index browses this page by page. Identical for every visitor, so it is
    /// cached the same way the quest index is; the caller's own votes come from my-votes below.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<NpcListResponse>> List(
        [FromQuery] NpcSearchRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(await pageService.ListAsync(request, cancellationToken));
    }

    /// <summary>The caller's standing votes among the NPCs a listing page is showing.</summary>
    [HttpGet("my-votes")]
    public async Task<ActionResult<NpcVotesResponse>> MyVotes(
        [FromQuery] int[] npcIds,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await pageService.GetVotesAsync(npcIds ?? [], User, ConnectionIp, cancellationToken));
    }

    [HttpGet("{npcId:int}")]
    public async Task<ActionResult<NpcDetailResponse>> Get(int npcId, CancellationToken cancellationToken)
    {
        var npc = await pageService.GetAsync(npcId, cancellationToken);
        if (npc is null)
        {
            return NotFound();
        }

        // Identical for every visitor; the caller's own vote is served by my-vote below.
        Response.Headers.CacheControl = "public, max-age=60";
        return Ok(npc);
    }

    [HttpGet("{npcId:int}/my-vote")]
    public async Task<ActionResult<MyNpcVoteResponse>> MyVote(int npcId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var vote = await pageService.GetVoteAsync(npcId, User, ConnectionIp, cancellationToken);
        return vote is null ? NotFound() : Ok(vote);
    }

    [HttpGet("{npcId:int}/recordings")]
    public async Task<ActionResult<NpcRecordingsResponse>> Recordings(int npcId, CancellationToken cancellationToken)
    {
        var recordings = await recordingService.GetRecordingsAsync(npcId, cancellationToken);
        return recordings is null ? NotFound() : Ok(recordings);
    }

    [HttpPut("{npcId:int}/vote")]
    public async Task<ActionResult<NpcVoteResponse>> Vote(
        int npcId,
        [FromBody] NpcVoteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Vote is null)
        {
            ModelState.AddModelError(nameof(request.Vote), "A vote must be either 'up' or 'down'.");
            return ValidationProblem(ModelState);
        }

        return Result(await voteService.SetVoteAsync(
            npcId, request.Vote.Value, User, ConnectionIp, cancellationToken));
    }

    [HttpDelete("{npcId:int}/vote")]
    public async Task<ActionResult<NpcVoteResponse>> ClearVote(int npcId, CancellationToken cancellationToken) =>
        Result(await voteService.ClearVoteAsync(npcId, User, ConnectionIp, cancellationToken));

    [HttpGet("{npcId:int}/comments")]
    public async Task<ActionResult<NpcCommentsResponse>> Comments(int npcId, CancellationToken cancellationToken)
    {
        // Whether a comment may be deleted depends on who is asking, so this must never be cached.
        Response.Headers.CacheControl = "no-store";
        var comments = await commentService.GetCommentsAsync(npcId, User, cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HttpPost("{npcId:int}/comments")]
    public async Task<ActionResult<NpcCommentResponse>> PostComment(
        int npcId,
        [FromBody] PostNpcCommentRequest request,
        CancellationToken cancellationToken) =>
        Result(await commentService.PostAsync(npcId, request, User, ConnectionIp, cancellationToken));

    [HttpDelete("{npcId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> DeleteComment(
        int npcId,
        int commentId,
        CancellationToken cancellationToken)
    {
        var result = await commentService.DeleteAsync(npcId, commentId, User, cancellationToken);
        return result.Succeeded ? NoContent() : Problem(result);
    }

    private ActionResult<T> Result<T>(NpcWriteResult<T> result) =>
        result.Succeeded ? Ok(result.Value!) : Problem(result);

    private ActionResult Problem<T>(NpcWriteResult<T> result)
    {
        if (!result.Found)
        {
            return NotFound();
        }

        if (result.IsForbidden)
        {
            return Forbid();
        }

        if (result.IsRateLimited)
        {
            // Budgets reset on the hour, so the wait is the remainder of the current one.
            var now = DateTime.UtcNow;
            var seconds = 3600 - ((now.Minute * 60) + now.Second);
            Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = "Too many requests. Retry after the indicated delay.", retryAfterSeconds = seconds });
        }

        ModelState.AddErrors(result.Errors ?? new Dictionary<string, string>());
        return ValidationProblem(ModelState);
    }
}
