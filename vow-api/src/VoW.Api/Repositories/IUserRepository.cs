using VoW.Api.Domain.Users;

namespace VoW.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetAdminByDiscordIdAsync(string discordId, CancellationToken cancellationToken);

    Task<User?> GetAdminByUserIdAsync(int userId, CancellationToken cancellationToken);
}
