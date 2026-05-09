using VoW.Api.Contracts.Accounts;

namespace VoW.Api.Services.Accounts;

public interface IAccountService
{
    Task<IReadOnlyCollection<AccountRoleResponse>> GetRolesAsync(CancellationToken cancellationToken);

    Task<AccountSearchServiceResult> SearchAsync(AccountSearchRequest request, CancellationToken cancellationToken);

    Task<AccountDetailsResponse?> GetAsync(int userId, CancellationToken cancellationToken);

    Task<AccountMutationResult> UpdateAsync(int userId, UpdateAccountRequest request, CancellationToken cancellationToken);

    Task<AccountMutationResult> ReplaceRolesAsync(int userId, UpdateAccountRolesRequest request, CancellationToken cancellationToken);

    Task<AccountMutationResult> UploadAvatarAsync(int userId, Stream image, CancellationToken cancellationToken);

    Task<AccountMutationResult> ClearAvatarAsync(int userId, CancellationToken cancellationToken);

    Task<ResetPasswordServiceResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken);

    Task<AccountMutationResult> DeleteAsync(int userId, CancellationToken cancellationToken);
}
