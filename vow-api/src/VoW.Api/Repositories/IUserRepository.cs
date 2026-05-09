using VoW.Api.Domain.Users;

namespace VoW.Api.Repositories;

public interface IUserRepository
{
    Task<UserProfile?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken);

    Task<UserProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);
}
