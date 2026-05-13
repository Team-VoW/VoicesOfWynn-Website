using VoW.Api.Domain.Accounts;

namespace VoW.Api.Repositories;

public interface IAccountRepository
{
    Task<IReadOnlyCollection<AccountRole>> GetRolesAsync(CancellationToken cancellationToken);

    Task<AccountSearchPage> SearchAsync(AccountSearchCriteria criteria, CancellationToken cancellationToken);

    Task<AccountDetails?> GetAsync(int userId, CancellationToken cancellationToken);

    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken);

    Task<bool> IsSystemAdminAsync(int userId, CancellationToken cancellationToken);

    Task<bool> DisplayNameExistsAsync(int exceptUserId, string displayName, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(int exceptUserId, string email, CancellationToken cancellationToken);

    Task<bool> DiscordIdExistsAsync(int exceptUserId, string discordId, CancellationToken cancellationToken);

    Task<bool> SocialExistsAsync(int exceptUserId, string column, string value, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(int userId, UpdateAccountCommand command, CancellationToken cancellationToken);

    Task<bool> UpdateSelfProfileAsync(int userId, UpdateSelfProfileCommand command, CancellationToken cancellationToken);

    Task<string?> GetPasswordHashAsync(int userId, CancellationToken cancellationToken);

    Task<bool> SetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken);

    Task<int> InsertAsync(CreateAccountCommand command, CancellationToken cancellationToken);

    Task<bool> ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken);

    Task<bool> SetAvatarAsync(int userId, string picture, PictureType pictureType, CancellationToken cancellationToken);

    Task<bool> ClearAvatarAsync(int userId, CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken);
}
