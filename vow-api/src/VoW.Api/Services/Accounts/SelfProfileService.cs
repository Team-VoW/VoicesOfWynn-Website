using MySqlConnector;
using VoW.Api.Contracts.Profile;
using VoW.Api.Domain.Accounts;
using VoW.Api.Repositories;
using VoW.Api.Services.Security;

namespace VoW.Api.Services.Accounts;

internal sealed class SelfProfileService(
    IAccountRepository accountRepository,
    AccountProfileValidator validator,
    AccountAvatarManager avatarManager) : ISelfProfileService
{
    public async Task<SelfProfileResponse?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetAsync(userId, cancellationToken);
        return account is null
            ? null
            : new SelfProfileResponse(
                account.UserId,
                account.DisplayName,
                account.AvatarUrl,
                account.DefaultAvatarUrl,
                account.Email,
                account.PublicEmail,
                account.Discord,
                account.Youtube,
                account.Twitter,
                account.CastingCallClub,
                account.Bio,
                account.Lore,
                account.ForcePasswordChange);
    }

    public async Task<AccountMutationResult> UpdateAsync(
        int userId,
        UpdateSelfProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!await accountRepository.UserExistsAsync(userId, cancellationToken))
        {
            return AccountMutationResult.NotFound();
        }

        var displayName = NormalizeRequired(request.DisplayName);
        var displayNameError = await validator.ValidateDisplayNameAsync(userId, displayName, cancellationToken);
        if (displayNameError is not null)
        {
            return AccountMutationResult.Invalid(displayNameError.Field, displayNameError.Message);
        }

        var email = NormalizeOptional(request.Email);
        var emailError = await validator.ValidateEmailAsync(userId, email, cancellationToken);
        if (emailError is not null)
        {
            return AccountMutationResult.Invalid(emailError.Field, emailError.Message);
        }

        var discord = NormalizeOptional(request.Discord);
        var discordError = await validator.ValidateDiscordHandleAsync(userId, discord, cancellationToken);
        if (discordError is not null)
        {
            return AccountMutationResult.Invalid(discordError.Field, discordError.Message);
        }

        var youtube = NormalizeOptional(request.Youtube);
        var youtubeError = await validator.ValidateYoutubeAsync(userId, youtube, cancellationToken);
        if (youtubeError is not null)
        {
            return AccountMutationResult.Invalid(youtubeError.Field, youtubeError.Message);
        }

        var twitter = NormalizeOptional(request.Twitter);
        var twitterError = await validator.ValidateTwitterAsync(userId, twitter, cancellationToken);
        if (twitterError is not null)
        {
            return AccountMutationResult.Invalid(twitterError.Field, twitterError.Message);
        }

        var castingCallClub = NormalizeOptional(request.CastingCallClub);
        var cccError = await validator.ValidateCastingCallClubAsync(userId, castingCallClub, cancellationToken);
        if (cccError is not null)
        {
            return AccountMutationResult.Invalid(cccError.Field, cccError.Message);
        }

        var bio = NormalizeOptional(request.Bio);
        var bioError = validator.ValidateBio(bio);
        if (bioError is not null)
        {
            return AccountMutationResult.Invalid(bioError.Field, bioError.Message);
        }

        var lore = NormalizeOptional(request.Lore);
        var loreError = validator.ValidateLore(lore);
        if (loreError is not null)
        {
            return AccountMutationResult.Invalid(loreError.Field, loreError.Message);
        }

        try
        {
            return await accountRepository.UpdateSelfProfileAsync(
                userId,
                new UpdateSelfProfileCommand(
                    displayName!,
                    email,
                    request.PublicEmail,
                    discord,
                    youtube,
                    twitter,
                    castingCallClub,
                    bio,
                    lore),
                cancellationToken)
                ? AccountMutationResult.Success()
                : AccountMutationResult.NotFound();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return AccountMutationResult.Invalid(nameof(request.DisplayName), "Account update conflicts with existing account data.");
        }
    }

    public async Task<AccountMutationResult> SetPasswordAsync(
        int userId,
        SetSelfPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var currentHash = await accountRepository.GetPasswordHashAsync(userId, cancellationToken);
        if (currentHash is null)
        {
            return AccountMutationResult.NotFound();
        }

        if (string.IsNullOrEmpty(request.OldPassword))
        {
            return AccountMutationResult.Invalid(nameof(request.OldPassword), "Current password is required.");
        }

        if (!AccountPasswordHasher.Verify(request.OldPassword, currentHash))
        {
            return AccountMutationResult.Invalid(nameof(request.OldPassword), "Current password is incorrect.");
        }

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < AccountProfileValidator.PasswordMinLength)
        {
            return AccountMutationResult.Invalid(nameof(request.NewPassword), $"Password must be at least {AccountProfileValidator.PasswordMinLength} characters long.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return AccountMutationResult.Invalid(nameof(request.ConfirmNewPassword), "New passwords do not match.");
        }

        return await accountRepository.SetPasswordAsync(
            userId,
            AccountPasswordHasher.Hash(request.NewPassword),
            cancellationToken)
            ? AccountMutationResult.Success()
            : AccountMutationResult.NotFound();
    }

    public Task<AccountMutationResult> UploadAvatarAsync(
        int userId,
        Stream image,
        CancellationToken cancellationToken) =>
        avatarManager.UploadAvatarAsync(userId, image, cancellationToken);

    public Task<AccountMutationResult> ClearAvatarAsync(int userId, CancellationToken cancellationToken) =>
        avatarManager.ClearAvatarAsync(userId, cancellationToken);

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeRequired(string? value) => value?.Trim();
}
