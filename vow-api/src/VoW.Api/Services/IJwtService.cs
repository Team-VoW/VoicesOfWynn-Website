using System.Security.Claims;
using VoW.Api.Models;

namespace VoW.Api.Services;

public interface IJwtService
{
    AuthTokenResponse CreateTokenPair(User user);

    RefreshTokenResponse CreateAccessToken(User user);

    ClaimsPrincipal ValidateRefreshToken(string token);
}
