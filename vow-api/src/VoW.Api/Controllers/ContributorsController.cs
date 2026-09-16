using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Contributors;
using VoW.Api.Services.Contributors;

namespace VoW.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("contributors")]
public sealed class ContributorsController(IContributorService contributorService) : ControllerBase
{
    // These two responses are identical for every visitor, so they are safe to cache briefly.
    // Anything that varies by caller lives on the my-votes endpoint instead.
    private const string PublicCache = "public, max-age=60";

    [HttpGet]
    public async Task<ActionResult<ContributorListResponse>> List(
        [FromQuery] ContributorSearchRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = PublicCache;
        return Ok(await contributorService.ListAsync(request, cancellationToken));
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<ContributorDetailResponse>> Get(int userId, CancellationToken cancellationToken)
    {
        var contributor = await contributorService.GetAsync(userId, cancellationToken);
        if (contributor is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = PublicCache;
        return Ok(contributor);
    }

    /// <summary>
    /// The caller's own votes on this contributor's NPCs. Split from the profile so that response
    /// is the same for everyone and stays cacheable.
    /// </summary>
    [HttpGet("{userId:int}/my-votes")]
    public async Task<ActionResult<ContributorVotesResponse>> MyVotes(int userId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var votes = await contributorService.GetVotesAsync(userId, User, ipAddress, cancellationToken);
        return votes is null ? NotFound() : Ok(votes);
    }
}
