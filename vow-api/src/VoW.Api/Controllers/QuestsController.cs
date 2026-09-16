using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Services.Contents;

namespace VoW.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("quests")]
public sealed class QuestsController(IQuestPageService questService) : ControllerBase
{
    // The index and a quest's cast look the same to every visitor, so they are safe to cache
    // briefly. Anything that varies by caller lives on the my-votes endpoint instead.
    private const string PublicCache = "public, max-age=60";

    [HttpGet]
    public async Task<ActionResult<QuestListResponse>> List(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = PublicCache;
        return Ok(await questService.ListAsync(cancellationToken));
    }

    /// <remarks>
    /// Quests are addressed by their degenerated name, the same URL the legacy site used, so links
    /// that have been shared for years keep working.
    /// </remarks>
    [HttpGet("{degeneratedName}")]
    public async Task<ActionResult<QuestDetailResponse>> Get(
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        var quest = await questService.GetAsync(degeneratedName, cancellationToken);
        if (quest is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = PublicCache;
        return Ok(quest);
    }

    [HttpGet("{degeneratedName}/my-votes")]
    public async Task<ActionResult<NpcVotesResponse>> MyVotes(
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var votes = await questService.GetVotesAsync(degeneratedName, User, ipAddress, cancellationToken);
        return votes is null ? NotFound() : Ok(votes);
    }
}
