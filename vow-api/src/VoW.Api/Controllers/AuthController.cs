using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Contracts.Auth;
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
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string OAuthStateCookiePrefix = "vow_oauth_state_";
    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(10);

    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        var authProvider = FindProvider(provider);
        if (authProvider is null)
        {
            return NotFound();
        }

        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        Response.Cookies.Append(CreateOAuthStateCookieName(state), state, BuildStateCookieOptions(OAuthStateLifetime));
        return Redirect(authProvider.BuildLoginUrl(state));
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery(Name = "error")] string? providerError,
        CancellationToken cancellationToken)
    {
        var authProvider = FindProvider(provider);
        if (authProvider is null)
        {
            return RedirectToSpaCallbackError("invalid_provider");
        }

        var stateCookieName = string.IsNullOrEmpty(state) ? null : CreateOAuthStateCookieName(state);
        var expectedState = stateCookieName is null ? null : Request.Cookies[stateCookieName];
        // Always clear the cookie - it's single-use either way.
        if (stateCookieName is not null)
        {
            Response.Cookies.Delete(stateCookieName, BuildStateCookieOptions(TimeSpan.Zero));
        }

        if (string.IsNullOrEmpty(state)
            || string.IsNullOrEmpty(expectedState)
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(state),
                System.Text.Encoding.ASCII.GetBytes(expectedState)))
        {
            logger.LogWarning("OAuth state mismatch on {Provider} callback.", provider);
            return RedirectToSpaCallbackError("invalid_oauth_state");
        }

        if (!string.IsNullOrWhiteSpace(providerError))
        {
            return RedirectToSpaCallbackError(providerError);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToSpaCallbackError("missing_authorization_code");
        }

        try
        {
            var externalUser = await authProvider.ExchangeCodeForIdentityAsync(code, cancellationToken);
            var user = externalUser.Provider == "discord"
                ? await userRepository.GetAdminByDiscordIdAsync(externalUser.Id, cancellationToken)
                : null;

            if (user is null)
            {
                return RedirectToSpaCallbackError("admin_required");
            }

            var handoffCode = handoffService.Create(jwtService.CreateTokenPair(user));
            var callbackUrl = QueryHelpers.AddQueryString(GetSpaCallbackUrl(), "code", handoffCode);
            return Redirect(callbackUrl);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "External OAuth request failed.");
            return RedirectToSpaCallbackError("external_oauth_failed");
        }
    }

    [HttpPost("handoff")]
    public ActionResult<AuthTokenResponse> Handoff([FromBody] AuthHandoffRequest request)
    {
        var tokens = handoffService.Consume(request.Code);
        return tokens is null ? Unauthorized() : Ok(tokens);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(
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
            // Rotate the refresh token alongside the access token (RFC 9700 §4.14).
            // Note: without server-side revocation, the previous refresh token is still
            // technically replayable until it expires; full reuse-detection requires
            // persisted token state.
            return user is null ? Unauthorized() : Ok(jwtService.CreateTokenPair(user));
        }
        catch (SecurityTokenException)
        {
            return Unauthorized();
        }
    }

    private IExternalAuthProvider? FindProvider(string provider) =>
        authProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, provider, StringComparison.OrdinalIgnoreCase));

    private static string CreateOAuthStateCookieName(string state) => $"{OAuthStateCookiePrefix}{state}";

    private IActionResult RedirectToSpaCallbackError(string error)
    {
        var errorUrl = QueryHelpers.AddQueryString(GetSpaCallbackUrl(), "error", error);
        return Redirect(errorUrl);
    }

    private CookieOptions BuildStateCookieOptions(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        // Lax (not Strict) so the cookie is sent on the top-level redirect back from the OAuth provider.
        SameSite = SameSiteMode.Lax,
        Path = "/auth",
        MaxAge = lifetime,
        IsEssential = true
    };

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
