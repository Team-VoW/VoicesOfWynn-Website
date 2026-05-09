using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Models;

namespace VoW.Api.Services;

public sealed class JwtService(IConfiguration configuration, IHostEnvironment environment) : IJwtService
{
    public const string AccessTokenType = "access";
    public const string RefreshTokenType = "refresh";
    public const string DevelopmentJwtSecret = "development-only-secret-change-before-running-anywhere";

    private readonly SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(GetJwtSecret(configuration, environment)));

    public AuthTokenResponse CreateTokenPair(User user)
    {
        var access = CreateAccessToken(user);
        var refresh = CreateToken(
            [
                new Claim("type", RefreshTokenType),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString())
            ],
            TimeSpan.FromDays(30));

        return new AuthTokenResponse(access.token, refresh.token, access.expiresAt);
    }

    private (string token, DateTimeOffset expiresAt) CreateAccessToken(User user) =>
        CreateToken(
            [
                new Claim("type", AccessTokenType),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim("discord_id", user.DiscordId),
                new Claim("display_name", user.DisplayName)
            ],
            TimeSpan.FromHours(1));

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

    public static string GetJwtSecret(IConfiguration configuration, IHostEnvironment environment)
    {
        var jwtSecret = configuration["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            if (environment.IsDevelopment())
            {
                return DevelopmentJwtSecret;
            }

            throw new InvalidOperationException("JWT_SECRET must be configured outside Development.");
        }

        if (!environment.IsDevelopment() && jwtSecret == DevelopmentJwtSecret)
        {
            throw new InvalidOperationException("JWT_SECRET cannot use the development fallback value outside Development.");
        }

        return jwtSecret;
    }
}
