using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Models;
using VoW.Api.Repositories;
using VoW.Api.Services;

namespace VoW.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IEnumerable<IExternalAuthProvider> authProviders,
    IUserRepository userRepository,
    IJwtService jwtService,
    IAuthHandoffService handoffService,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        var authProvider = FindProvider(provider);
        return authProvider is null ? NotFound() : Redirect(authProvider.BuildLoginUrl());
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        var authProvider = FindProvider(provider);
        if (authProvider is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new ProblemDetails { Title = "Authorization code is required." });
        }

        try
        {
            var externalUser = await authProvider.ExchangeCodeForIdentityAsync(code, cancellationToken);
            var user = externalUser.Provider == "discord"
                ? await userRepository.GetAdminByDiscordIdAsync(externalUser.Id, cancellationToken)
                : null;

            if (user is null)
            {
                return Forbid();
            }

            var handoffCode = handoffService.Create(jwtService.CreateTokenPair(user));
            var callbackUrl = QueryHelpers.AddQueryString(GetSpaCallbackUrl(), "code", handoffCode);
            return Redirect(callbackUrl);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "External OAuth request failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails { Title = "External OAuth request failed." });
        }
    }

    [HttpPost("handoff")]
    public ActionResult<AuthTokenResponse> Handoff([FromBody] AuthHandoffRequest request)
    {
        var tokens = handoffService.Consume(request.Code);
        return tokens is null ? Unauthorized() : Ok(tokens);
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

    private IExternalAuthProvider? FindProvider(string provider) =>
        authProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, provider, StringComparison.OrdinalIgnoreCase));

    private string GetSpaCallbackUrl()
    {
        var configured = configuration["SPA_AUTH_CALLBACK_URL"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var origin = configuration["CORS_ORIGIN"] ?? "https://app.voicesofwynn.com";
        return $"{origin.TrimEnd('/')}/auth/callback";
    }
}
