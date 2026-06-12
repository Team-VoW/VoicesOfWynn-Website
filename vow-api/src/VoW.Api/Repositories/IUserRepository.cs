using VoW.Api.Domain.Users;
using VoW.Api.Domain.Auth;

namespace VoW.Api.Repositories;

public interface IUserRepository
{
    Task<UserProfile?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken);

    Task<UserProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);

    Task<PasswordLoginUser?> GetForPasswordLoginAsync(string username, CancellationToken cancellationToken);
}
