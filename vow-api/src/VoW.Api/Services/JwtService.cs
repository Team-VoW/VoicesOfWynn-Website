using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Models;

namespace VoW.Api.Services;

public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    public const string AccessTokenType = "access";
    public const string RefreshTokenType = "refresh";

    private readonly SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(GetJwtSecret(configuration)));

    public AuthTokenResponse CreateTokenPair(User user)
    {
        var access = CreateAccessToken(user);
        var refreshToken = CreateToken(
            [
                new Claim("type", RefreshTokenType),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString())
            ],
            TimeSpan.FromDays(30));

        return new AuthTokenResponse(access.AccessToken, refreshToken.token, access.ExpiresAt);
    }

    public RefreshTokenResponse CreateAccessToken(User user)
    {
        var token = CreateToken(
            [
                new Claim("type", AccessTokenType),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim("discord_id", user.DiscordId),
                new Claim("display_name", user.DisplayName)
            ],
            TimeSpan.FromHours(1));

        return new RefreshTokenResponse(token.token, token.expiresAt);
    }

    public ClaimsPrincipal ValidateRefreshToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, CreateTokenValidationParameters(signingKey), out _);
        var tokenType = principal.FindFirst("type")?.Value;

        if (tokenType != RefreshTokenType)
        {
            throw new SecurityTokenValidationException("Refresh token required.");
        }

        return principal;
    }

    public static TokenValidationParameters CreateTokenValidationParameters(SecurityKey signingKey) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    private (string token, DateTimeOffset expiresAt) CreateToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string GetJwtSecret(IConfiguration configuration)
    {
        var jwtSecret = configuration["JWT_SECRET"];
        return string.IsNullOrWhiteSpace(jwtSecret)
            ? "development-only-secret-change-before-running-anywhere"
            : jwtSecret;
    }
}
