using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.DiscordIntegration;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.DiscordIntegration;

namespace VoW.Api.Controllers;

[ApiController]
[RequireDiscordBotApiKey]
[Route("integrations/discord/users")]
public sealed class DiscordIntegrationController(IDiscordIntegrationService discordIntegrationService) : ControllerBase
{
    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyCollection<DiscordIntegrationUserResponse>>> Users(
        CancellationToken cancellationToken) =>
        Ok(await discordIntegrationService.GetUsersAsync(cancellationToken));

    [HttpPost("sync")]
    public async Task<ActionResult<SyncDiscordUserResponse>> Sync(
        [FromBody] SyncDiscordUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await discordIntegrationService.SyncUserAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddErrors(result.Errors);
            return ValidationProblem(ModelState);
        }

        return result.Response!.Created
            ? StatusCode(StatusCodes.Status201Created, result.Response)
            : Ok(result.Response);
    }

}
