using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;
using VoW.Api.Contracts.Accounts;
using VoW.Api.Domain.Accounts;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Accounts;

public sealed partial class AccountService(
    IAccountRepository accountRepository,
    IAccountAvatarStorage avatarStorage,
    ILogger<AccountService> logger) : IAccountService
{
    private const int DisplayNameMinLength = 3;
    private const int DisplayNameMaxLength = 31;
    private const int EmailMaxLength = 255;
    private const int DiscordIdMaxLength = 19;
    private const int PasswordMinLength = 6;
    private const int DiscordMinLength = 2;
    private const int DiscordMaxLength = 37;
    private const int YoutubeMaxLength = 56;
    private const int TwitterMaxLength = 15;
    private const int CastingCallClubMaxLength = 64;
    private const int BioMaxBytes = 65535;
    private const int LoreMaxLength = 63;
    private static readonly int[] AllowedPageSizes = [10, 25, 50, 100];

    private sealed record FieldError(string Field, string Message);

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
        var displayNameError = await ValidateDisplayNameAsync(userId, displayName, cancellationToken);
        if (displayNameError is not null)
        {
            return AccountMutationResult.Invalid(displayNameError.Field, displayNameError.Message);
        }

        var password = NormalizeOptional(request.Password);
        if (password is not null && password.Length < PasswordMinLength)
        {
            return AccountMutationResult.Invalid(nameof(request.Password), $"Password must be at least {PasswordMinLength} characters long.");
        }

        var discordId = NormalizeOptional(request.DiscordId);
        var discordIdError = await ValidateDiscordIdAsync(userId, discordId, cancellationToken);
        if (discordIdError is not null)
        {
            return AccountMutationResult.Invalid(discordIdError.Field, discordIdError.Message);
        }

        var email = NormalizeOptional(request.Email);
        if (email is not null)
        {
            if (email.Length > EmailMaxLength)
            {
                return AccountMutationResult.Invalid(nameof(request.Email), $"E-mail address must not be more than {EmailMaxLength} characters long.");
            }

            if (!EmailRegex().IsMatch(email))
            {
                return AccountMutationResult.Invalid(nameof(request.Email), "E-mail address is not in a valid format.");
            }

            if (await accountRepository.EmailExistsAsync(userId, email, cancellationToken))
            {
                return AccountMutationResult.Invalid(nameof(request.Email), "This e-mail address is already in use.");
            }
        }

        var discord = NormalizeOptional(request.Discord);
        var discordError = await ValidateDiscordHandleAsync(userId, discord, cancellationToken);
        if (discordError is not null)
        {
            return AccountMutationResult.Invalid(discordError.Field, discordError.Message);
        }

        var youtube = NormalizeOptional(request.Youtube);
        var youtubeError = await ValidateSocialAsync(
            userId,
            youtube,
            "youtube",
            nameof(request.Youtube),
            "YouTube channel link",
            null,
            YoutubeMaxLength,
            cancellationToken);
        if (youtubeError is not null)
        {
            return AccountMutationResult.Invalid(youtubeError.Field, youtubeError.Message);
        }

        var twitter = NormalizeOptional(request.Twitter);
        var twitterError = await ValidateSocialAsync(
            userId,
            twitter,
            "twitter",
            nameof(request.Twitter),
            "Twitter handle",
            null,
            TwitterMaxLength,
            cancellationToken);
        if (twitterError is not null)
        {
            return AccountMutationResult.Invalid(twitterError.Field, twitterError.Message);
        }

        var castingCallClub = NormalizeOptional(request.CastingCallClub);
        var cccError = await ValidateCastingCallClubAsync(userId, castingCallClub, cancellationToken);
        if (cccError is not null)
        {
            return AccountMutationResult.Invalid(cccError.Field, cccError.Message);
        }

        var bio = NormalizeOptional(request.Bio);
        if (bio is not null && Encoding.UTF8.GetByteCount(bio) > BioMaxBytes)
        {
            return AccountMutationResult.Invalid(nameof(request.Bio), $"Bio must not be more than {BioMaxBytes} bytes long.");
        }

        var lore = NormalizeOptional(request.Lore);
        if (lore is not null && lore.Length > LoreMaxLength)
        {
            return AccountMutationResult.Invalid(nameof(request.Lore), $"Lore must not be more than {LoreMaxLength} characters long.");
        }

        var passwordHash = password is null ? null : HashPassword(password);
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
        var displayNameError = await ValidateDisplayNameAsync(0, displayName, cancellationToken);
        if (displayNameError is not null)
        {
            return CreateAccountServiceResult.Invalid(displayNameError.Field, displayNameError.Message);
        }

        var discordId = NormalizeOptional(request.DiscordId);
        var discordIdError = await ValidateDiscordIdAsync(0, discordId, cancellationToken);
        if (discordIdError is not null)
        {
            return CreateAccountServiceResult.Invalid(discordIdError.Field, discordIdError.Message);
        }

        var discord = NormalizeOptional(request.Discord);
        var discordError = await ValidateDiscordHandleAsync(0, discord, cancellationToken);
        if (discordError is not null)
        {
            return CreateAccountServiceResult.Invalid(discordError.Field, discordError.Message);
        }

        var castingCallClub = NormalizeOptional(request.CastingCallClub);
        var cccError = await ValidateCastingCallClubAsync(0, castingCallClub, cancellationToken);
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
                    HashPassword(temporaryPassword),
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
        if (await accountRepository.SetAvatarAsync(userId, $"{userId}.webp", cancellationToken))
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
            return AccountMutationResult.NotFound();
        }

        await avatarStorage.DeleteCustomAvatarsAsync(userId, cancellationToken);
        return AccountMutationResult.Success();
    }

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
        return await accountRepository.ResetPasswordAsync(userId, HashPassword(temporaryPassword), cancellationToken)
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

    private async Task<FieldError?> ValidateDisplayNameAsync(
        int exceptUserId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var lengthError = ValidateLength(displayName, "Display name", DisplayNameMinLength, DisplayNameMaxLength);
        if (lengthError is not null)
        {
            return new FieldError("DisplayName", lengthError);
        }

        return await accountRepository.DisplayNameExistsAsync(exceptUserId, displayName!, cancellationToken)
            ? new FieldError("DisplayName", "This display name is already in use.")
            : null;
    }

    private async Task<FieldError?> ValidateDiscordIdAsync(
        int exceptUserId,
        string? discordId,
        CancellationToken cancellationToken)
    {
        if (discordId is null)
        {
            return null;
        }

        if (discordId.Length > DiscordIdMaxLength
            || !DiscordIdRegex().IsMatch(discordId)
            || !long.TryParse(discordId, out var parsedDiscordId)
            || parsedDiscordId <= 0)
        {
            return new FieldError("DiscordId", "Discord ID must be a positive numeric Discord user ID.");
        }

        return await accountRepository.DiscordIdExistsAsync(exceptUserId, discordId, cancellationToken)
            ? new FieldError("DiscordId", "This Discord ID is already linked by another user.")
            : null;
    }

    private async Task<FieldError?> ValidateDiscordHandleAsync(
        int exceptUserId,
        string? discord,
        CancellationToken cancellationToken)
    {
        var socialError = await ValidateSocialAsync(
            exceptUserId,
            discord,
            "discord",
            "Discord",
            "Discord username",
            DiscordMinLength,
            DiscordMaxLength,
            cancellationToken);
        if (socialError is not null)
        {
            return socialError;
        }

        if (discord is not null && (!DiscordRegex().IsMatch(discord) || discord.Contains("..", StringComparison.Ordinal)))
        {
            return new FieldError("Discord", "Discord username is in incorrect format.");
        }

        return null;
    }

    private Task<FieldError?> ValidateCastingCallClubAsync(
        int exceptUserId,
        string? castingCallClub,
        CancellationToken cancellationToken) =>
        ValidateSocialAsync(
            exceptUserId,
            castingCallClub,
            "castingcallclub",
            "CastingCallClub",
            "Casting Call Club name",
            null,
            CastingCallClubMaxLength,
            cancellationToken);

    private async Task<FieldError?> ValidateSocialAsync(
        int userId,
        string? value,
        string column,
        string field,
        string label,
        int? minLength,
        int maxLength,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return null;
        }

        var lengthError = ValidateLength(value, label, minLength, maxLength);
        if (lengthError is not null)
        {
            return new FieldError(field, lengthError);
        }

        return await accountRepository.SocialExistsAsync(userId, column, value, cancellationToken)
            ? new FieldError(field, $"{label} is already linked by another user.")
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeRequired(string? value) => value?.Trim();

    private static string? ValidateLength(string? value, string label, int? minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return $"{label} is required.";
        }

        if (value.Length > maxLength)
        {
            return $"{label} must not be more than {maxLength} characters long.";
        }

        if (minLength is not null && value.Length < minLength.Value)
        {
            return $"{label} must not be less than {minLength.Value} characters long.";
        }

        return null;
    }

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

    private static string HashPassword(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        return hash.StartsWith("$2a$", StringComparison.Ordinal) || hash.StartsWith("$2b$", StringComparison.Ordinal)
            ? $"$2y${hash[4..]}"
            : hash;
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(chars, 12);
    }

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[0-9a-z_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordRegex();

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordIdRegex();
}
