using VoW.Api.Contracts.Profile;

namespace VoW.Api.Services.Accounts;

public interface ISelfProfileService
{
    Task<SelfProfileResponse?> GetAsync(int userId, CancellationToken cancellationToken);

    Task<AccountMutationResult> UpdateAsync(int userId, UpdateSelfProfileRequest request, CancellationToken cancellationToken);

    Task<AccountMutationResult> SetPasswordAsync(int userId, SetSelfPasswordRequest request, CancellationToken cancellationToken);

    Task<AccountMutationResult> UploadAvatarAsync(int userId, Stream image, CancellationToken cancellationToken);

    Task<AccountMutationResult> ClearAvatarAsync(int userId, CancellationToken cancellationToken);
}
