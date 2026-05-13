using VoW.Api.Contracts.Auth;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Users;
using VoW.Api.Repositories;
using VoW.Api.Services.Security;

namespace VoW.Api.Services.Auth;

public sealed class PasswordAuthService(
    IUserRepository userRepository,
    IJwtService jwtService) : IPasswordAuthService
{
    private const string DummyBcryptHash = "$2a$10$7EqJtq98hPqEX7fNZaFWoOhiJ4q2DxHtBmyOufnc0B9VjLQdA0oTy";

    public async Task<PasswordAuthResult> LoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = Normalize(request.Username);
        var password = request.Password ?? string.Empty;
        if (username is null || string.IsNullOrEmpty(password))
        {
            return PasswordAuthResult.Unauthorized();
        }

        var loginUser = await userRepository.GetForPasswordLoginAsync(username, cancellationToken);
        var passwordHash = loginUser?.PasswordHash ?? DummyBcryptHash;
        if (!AccountPasswordHasher.Verify(password, passwordHash) || loginUser?.PasswordHash is null)
        {
            return PasswordAuthResult.Unauthorized();
        }

        return PasswordAuthResult.Success(
            jwtService.CreateTokenPair(BuildUser(loginUser.Profile)),
            loginUser.ForcePasswordChange);
    }

    private static User BuildUser(UserProfile profile)
    {
        var capabilities = CapabilityMapper.Map(profile.Roles).ToArray();
        return new User(profile.UserId, profile.DiscordId, profile.DisplayName, profile.Roles, capabilities);
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
