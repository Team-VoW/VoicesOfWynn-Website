using VoW.Api.Domain.DiscordIntegration;

namespace VoW.Api.Repositories;

public interface IDiscordIntegrationRepository
{
    Task<IReadOnlyCollection<DiscordIntegrationUser>> GetUsersAsync(CancellationToken cancellationToken);

    Task<DiscordSyncUser?> GetUserByDiscordIdAsync(string discordId, CancellationToken cancellationToken);

    Task<DiscordSyncUser?> GetUserByDiscordNameAsync(string discordName, CancellationToken cancellationToken);

    Task<bool> DisplayNameExistsAsync(int exceptUserId, string displayName, CancellationToken cancellationToken);

    Task<bool> DiscordIdExistsAsync(int exceptUserId, string discordId, CancellationToken cancellationToken);

    Task<bool> DiscordNameExistsAsync(int exceptUserId, string discordName, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> GetRoleIdsByNameAsync(CancellationToken cancellationToken);

    Task<int> InsertUserAsync(CreateDiscordSyncUserCommand command, CancellationToken cancellationToken);

    Task<bool> UpdateDiscordFieldsAsync(int userId, string discordId, string discordName, CancellationToken cancellationToken);

    Task<bool> SetDiscordAvatarAsync(int userId, string picture, CancellationToken cancellationToken);

    Task<bool> ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken);
}
