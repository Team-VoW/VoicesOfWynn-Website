using VoW.Api.Domain.Users;

namespace VoW.Api.Services.Auth;

public interface IUserAccessService
{
    Task<User?> GetAccessibleUserByDiscordIdAsync(string discordId, CancellationToken cancellationToken);

    Task<User?> GetAccessibleUserByUserIdAsync(int userId, CancellationToken cancellationToken);
}
