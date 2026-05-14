using VoW.Api.Domain.Accounts;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Accounts;

internal sealed class AccountAvatarManager(
    IAccountRepository accountRepository,
    IAccountAvatarStorage avatarStorage,
    ILogger<AccountAvatarManager> logger)
{
    public async Task<AccountMutationResult> UploadAvatarAsync(
        int userId,
        Stream image,
        CancellationToken cancellationToken)
    {
        if (!await accountRepository.UserExistsAsync(userId, cancellationToken))
        {
            return AccountMutationResult.NotFound();
        }

        MemoryStream webp;
        try
        {
            webp = await AvatarImagePipeline.NormalizeToWebpAsync(image, cancellationToken);
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            return AccountMutationResult.Invalid("file", "The uploaded file is not a recognized image.");
        }
        catch (SixLabors.ImageSharp.InvalidImageContentException)
        {
            return AccountMutationResult.Invalid("file", "The uploaded image is corrupted or could not be decoded.");
        }

        await using var normalized = webp;
        await avatarStorage.DeleteCustomAvatarsAsync(userId, cancellationToken);
        logger.LogInformation("Deleted existing custom avatars for user {UserId}.", userId);
        await avatarStorage.UploadAvatarAsync(userId, normalized, cancellationToken);
        logger.LogInformation("Uploaded replacement avatar for user {UserId}.", userId);
        if (await accountRepository.SetAvatarAsync(userId, $"{userId}.webp", PictureType.Manual, cancellationToken))
        {
            logger.LogInformation("Set avatar database value for user {UserId}.", userId);
            return AccountMutationResult.Success();
        }

        logger.LogWarning(
            "Uploaded avatar for user {UserId}, but the database avatar update did not affect an account.",
            userId);
        return AccountMutationResult.NotFound();
    }

    public async Task<AccountMutationResult> ClearAvatarAsync(int userId, CancellationToken cancellationToken)
    {
        if (!await accountRepository.UserExistsAsync(userId, cancellationToken))
        {
            return AccountMutationResult.NotFound();
        }

        if (!await accountRepository.ClearAvatarAsync(userId, cancellationToken))
        {
            logger.LogWarning(
                "Clearing avatar for user {UserId} affected no database rows; proceeding to purge storage.",
                userId);
        }

        await avatarStorage.DeleteCustomAvatarsAsync(userId, cancellationToken);
        return AccountMutationResult.Success();
    }
}
