using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Models;
using VoW.Api.Repositories;
using VoW.Api.Services;

namespace VoW.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IDiscordAuthService discordAuthService,
    IUserRepository userRepository,
    IJwtService jwtService,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login() => Redirect(discordAuthService.BuildLoginUrl());

    [HttpGet("callback")]
    public async Task<ActionResult<AuthTokenResponse>> Callback([FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new ProblemDetails { Title = "Discord authorization code is required." });
        }

        try
        {
            var discordUser = await discordAuthService.ExchangeCodeForUserAsync(code, cancellationToken);
            var user = await userRepository.GetAdminByDiscordIdAsync(discordUser.Id, cancellationToken);

            if (user is null)
            {
                return Forbid();
            }

            return Ok(jwtService.CreateTokenPair(user));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Discord OAuth request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails { Title = "Discord OAuth request failed." });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshTokenResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var principal = jwtService.ValidateRefreshToken(request.RefreshToken);
            var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!int.TryParse(subject, out var userId))
            {
                return Unauthorized();
            }

            var user = await userRepository.GetAdminByUserIdAsync(userId, cancellationToken);
            return user is null ? Unauthorized() : Ok(jwtService.CreateAccessToken(user));
        }
        catch (SecurityTokenException)
        {
            return Unauthorized();
        }
    }
}
