using VoW.Api.Domain.Users;
using VoW.Api.Domain.Auth;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Auth;

public sealed class UserAccessService(IUserRepository userRepository) : IUserAccessService
{
    public async Task<User?> GetAccessibleUserByDiscordIdAsync(string discordId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByDiscordIdAsync(discordId, cancellationToken);
        return BuildAccessibleUser(user);
    }

    public async Task<User?> GetAccessibleUserByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUserIdAsync(userId, cancellationToken);
        return BuildAccessibleUser(user);
    }

    private static User? BuildAccessibleUser(UserProfile? user)
    {
        if (user is null)
        {
            return null;
        }

        var capabilities = CapabilityMapper.Map(user.Roles).ToArray();
        return capabilities.Length == 0
            ? null
            : new User(user.UserId, user.DiscordId, user.DisplayName, user.Roles, capabilities);
    }
}
