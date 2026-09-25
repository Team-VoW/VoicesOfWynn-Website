using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Casting;
using VoW.Api.Controllers;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Casting;

namespace VoW.Api.Controllers.Bot;

/// <summary>
/// VowBot's /setuppoll for Discord castings: it creates the quest's round, then uploads the latest
/// audio from each audition thread. Replaces the old staff-channel reaction poll.
/// </summary>
[ApiController]
[RequireDiscordBotApiKey]
[Route("bot/casting")]
public sealed class CastingController(ICastingBotService botService) : ControllerBase
{
    private const long AuditionMaxSizeBytes = 50L * 1024 * 1024;

    [HttpPost("rounds")]
    public async Task<ActionResult<BotCastingRoundResponse>> EnsureRound(
        [FromBody] BotEnsureCastingRoundRequest request,
        CancellationToken cancellationToken)
    {
        var result = await botService.EnsureRoundAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Value);
    }

    [HttpPost("rounds/{roundId:int}/auditions")]
    [RequestSizeLimit(AuditionMaxSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = AuditionMaxSizeBytes)]
    public async Task<ActionResult<BotAuditionResponse>> AddAudition(
        int roundId,
        [FromForm] BotUploadAuditionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            ModelState.AddModelError(nameof(request.File), "An audio file is required.");
            return ValidationProblem(ModelState);
        }

        await using var stream = request.File.OpenReadStream();
        var result = await botService.AddAuditionAsync(roundId, request, stream, cancellationToken);
        if (!result.Result.Found)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Result.Errors);
            return ValidationProblem(ModelState);
        }

        return Ok(result.Value);
    }
}
