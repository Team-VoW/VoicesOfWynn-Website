using System.Security.Cryptography;
using MySqlConnector;
using VoW.Api.Contracts.Accounts;
using VoW.Api.Domain.Accounts;
using VoW.Api.Repositories;
using VoW.Api.Services.Security;

namespace VoW.Api.Services.Accounts;

internal sealed class AccountAdminService(
    IAccountRepository accountRepository,
    AccountProfileValidator validator,
    AccountAvatarManager avatarManager) : IAccountAdminService
{
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];

    public async Task<IReadOnlyCollection<AccountRoleResponse>> GetRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await accountRepository.GetRolesAsync(cancellationToken);
        return roles.Select(role => new AccountRoleResponse(role.Id, role.Name, role.Color, role.Weight)).ToArray();
    }

    public async Task<AccountSearchServiceResult> SearchAsync(
        AccountSearchRequest request,
        CancellationToken cancellationToken)
    {
        var pageSize = AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : 25;
        var page = Math.Max(1, request.Page);
        var query = NormalizeOptional(request.Query);
        var result = await accountRepository.SearchAsync(new AccountSearchCriteria(query, page, pageSize), cancellationToken);

        return AccountSearchServiceResult.Success(new AccountSearchResponse(
            result.Total,
            result.Page,
            result.PageSize,
            result.Results.Select(account => new AccountSearchResult(
                account.UserId,
                account.DisplayName,
                account.AvatarUrl,
                account.DefaultAvatarUrl,
                SocialSummary(account),
                account.RoleIds)).ToArray()));
    }

    public async Task<AccountDetailsResponse?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetAsync(userId, cancellationToken);
        return account is null
            ? null
            : new AccountDetailsResponse(
                account.UserId,
                account.DisplayName,
                account.AvatarUrl,
                account.DefaultAvatarUrl,
                account.DiscordId,
                account.Email,
                account.PublicEmail,
                account.Discord,
                account.Youtube,
                account.Twitter,
                account.CastingCallClub,
                account.Bio,
                account.Lore,
                account.ForcePasswordChange,
                account.SystemAdmin,
                account.RoleIds);
    }

    public async Task<AccountMutationResult> UpdateAsync(
        int userId,
        UpdateAccountRequest request,
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

        var password = NormalizeOptional(request.Password);
        if (password is not null && password.Length < AccountProfileValidator.PasswordMinLength)
        {
            return AccountMutationResult.Invalid(nameof(request.Password), $"Password must be at least {AccountProfileValidator.PasswordMinLength} characters long.");
        }

        var discordId = NormalizeOptional(request.DiscordId);
        var discordIdError = await validator.ValidateDiscordIdAsync(userId, discordId, cancellationToken);
        if (discordIdError is not null)
        {
            return AccountMutationResult.Invalid(discordIdError.Field, discordIdError.Message);
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

        var bio = AccountBioSanitizer.Sanitize(NormalizeOptional(request.Bio));
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

        var passwordHash = password is null ? null : AccountPasswordHasher.Hash(password);
        try
        {
            return await accountRepository.UpdateAsync(
                userId,
                new UpdateAccountCommand(
                    displayName!,
                    passwordHash,
                    discordId,
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

    public async Task<CreateAccountServiceResult> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var displayName = NormalizeRequired(request.DisplayName);
        var displayNameError = await validator.ValidateDisplayNameAsync(0, displayName, cancellationToken);
        if (displayNameError is not null)
        {
            return CreateAccountServiceResult.Invalid(displayNameError.Field, displayNameError.Message);
        }

        var discordId = NormalizeOptional(request.DiscordId);
        var discordIdError = await validator.ValidateDiscordIdAsync(0, discordId, cancellationToken);
        if (discordIdError is not null)
        {
            return CreateAccountServiceResult.Invalid(discordIdError.Field, discordIdError.Message);
        }

        var discord = NormalizeOptional(request.Discord);
        var discordError = await validator.ValidateDiscordHandleAsync(0, discord, cancellationToken);
        if (discordError is not null)
        {
            return CreateAccountServiceResult.Invalid(discordError.Field, discordError.Message);
        }

        var castingCallClub = NormalizeOptional(request.CastingCallClub);
        var cccError = await validator.ValidateCastingCallClubAsync(0, castingCallClub, cancellationToken);
        if (cccError is not null)
        {
            return CreateAccountServiceResult.Invalid(cccError.Field, cccError.Message);
        }

        var temporaryPassword = GenerateTemporaryPassword();
        try
        {
            var userId = await accountRepository.InsertAsync(
                new CreateAccountCommand(
                    displayName!,
                    AccountPasswordHasher.Hash(temporaryPassword),
                    discordId,
                    discord,
                    castingCallClub),
                cancellationToken);
            return CreateAccountServiceResult.Success(userId, temporaryPassword);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return CreateAccountServiceResult.Invalid(nameof(request.DisplayName), "Account creation conflicts with existing account data.");
        }
    }

    public async Task<AccountMutationResult> ReplaceRolesAsync(
        int userId,
        UpdateAccountRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (!await accountRepository.UserExistsAsync(userId, cancellationToken))
        {
            return AccountMutationResult.NotFound();
        }

        var requestedRoleIds = request.RoleIds.Distinct().ToArray();
        var validRoleIds = (await accountRepository.GetRolesAsync(cancellationToken)).Select(role => role.Id).ToHashSet();
        if (requestedRoleIds.Any(roleId => !validRoleIds.Contains(roleId)))
        {
            return AccountMutationResult.Invalid(nameof(request.RoleIds), "Every selected role must exist.");
        }

        return await accountRepository.ReplaceRolesAsync(userId, requestedRoleIds, cancellationToken)
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

    public async Task<ResetPasswordServiceResult> ResetPasswordAsync(int userId, CancellationToken cancellationToken)
    {
        var account = await accountRepository.GetAsync(userId, cancellationToken);
        if (account is null)
        {
            return ResetPasswordServiceResult.NotFound();
        }

        if (account.SystemAdmin)
        {
            return ResetPasswordServiceResult.Invalid(nameof(userId), "Password of a system administrator cannot be reset this way.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        return await accountRepository.ResetPasswordAsync(userId, AccountPasswordHasher.Hash(temporaryPassword), cancellationToken)
            ? ResetPasswordServiceResult.Success(temporaryPassword)
            : ResetPasswordServiceResult.NotFound();
    }

    public async Task<AccountMutationResult> DeleteAsync(int userId, int callerId, CancellationToken cancellationToken)
    {
        if (!await accountRepository.UserExistsAsync(userId, cancellationToken))
        {
            return AccountMutationResult.NotFound();
        }

        if (userId == callerId || await accountRepository.IsSystemAdminAsync(userId, cancellationToken))
        {
            return AccountMutationResult.Forbidden();
        }

        return await accountRepository.DeleteAsync(userId, cancellationToken)
            ? AccountMutationResult.Success()
            : AccountMutationResult.NotFound();
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeRequired(string? value) => value?.Trim();

    private static string SocialSummary(AccountSummary account)
    {
        var parts = new[]
        {
            account.Email,
            account.Discord,
            account.Youtube,
            account.Twitter is null ? null : $"@{account.Twitter}",
            account.CastingCallClub,
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" · ", parts);
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(chars, 12);
    }
}
