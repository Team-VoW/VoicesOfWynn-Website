using System.Security.Claims;
using VoW.Api.Contracts.Auth;
using VoW.Api.Domain.Users;

namespace VoW.Api.Services;

public interface IJwtService
{
    AuthTokenResponse CreateTokenPair(User user);

    ClaimsPrincipal ValidateRefreshToken(string token);
}
