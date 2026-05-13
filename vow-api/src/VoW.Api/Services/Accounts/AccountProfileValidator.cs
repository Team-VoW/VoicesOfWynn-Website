using System.Text;
using System.Text.RegularExpressions;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Accounts;

internal sealed partial class AccountProfileValidator(IAccountRepository accountRepository)
{
    public const int PasswordMinLength = 6;

    private const int DisplayNameMinLength = 3;
    private const int DisplayNameMaxLength = 31;
    private const int EmailMaxLength = 255;
    private const int DiscordIdMaxLength = 19;
    private const int DiscordMinLength = 2;
    private const int DiscordMaxLength = 37;
    private const int YoutubeMaxLength = 56;
    private const int TwitterMaxLength = 15;
    private const int CastingCallClubMaxLength = 64;
    private const int BioMaxBytes = 65535;
    private const int LoreMaxLength = 63;

    public async Task<AccountFieldError?> ValidateDisplayNameAsync(
        int exceptUserId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var lengthError = ValidateLength(displayName, "Display name", DisplayNameMinLength, DisplayNameMaxLength);
        if (lengthError is not null)
        {
            return new AccountFieldError("DisplayName", lengthError);
        }

        return await accountRepository.DisplayNameExistsAsync(exceptUserId, displayName!, cancellationToken)
            ? new AccountFieldError("DisplayName", "This display name is already in use.")
            : null;
    }

    public async Task<AccountFieldError?> ValidateDiscordIdAsync(
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
            return new AccountFieldError("DiscordId", "Discord ID must be a positive numeric Discord user ID.");
        }

        return await accountRepository.DiscordIdExistsAsync(exceptUserId, discordId, cancellationToken)
            ? new AccountFieldError("DiscordId", "This Discord ID is already linked by another user.")
            : null;
    }

    public async Task<AccountFieldError?> ValidateEmailAsync(
        int exceptUserId,
        string? email,
        CancellationToken cancellationToken)
    {
        if (email is null)
        {
            return null;
        }

        if (email.Length > EmailMaxLength)
        {
            return new AccountFieldError("Email", $"E-mail address must not be more than {EmailMaxLength} characters long.");
        }

        if (!EmailRegex().IsMatch(email))
        {
            return new AccountFieldError("Email", "E-mail address is not in a valid format.");
        }

        return await accountRepository.EmailExistsAsync(exceptUserId, email, cancellationToken)
            ? new AccountFieldError("Email", "This e-mail address is already in use.")
            : null;
    }

    public async Task<AccountFieldError?> ValidateDiscordHandleAsync(
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
            return new AccountFieldError("Discord", "Discord username is in incorrect format.");
        }

        return null;
    }

    public Task<AccountFieldError?> ValidateCastingCallClubAsync(
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

    public Task<AccountFieldError?> ValidateYoutubeAsync(
        int exceptUserId,
        string? youtube,
        CancellationToken cancellationToken) =>
        ValidateSocialAsync(
            exceptUserId,
            youtube,
            "youtube",
            "Youtube",
            "YouTube channel link",
            null,
            YoutubeMaxLength,
            cancellationToken);

    public Task<AccountFieldError?> ValidateTwitterAsync(
        int exceptUserId,
        string? twitter,
        CancellationToken cancellationToken) =>
        ValidateSocialAsync(
            exceptUserId,
            twitter,
            "twitter",
            "Twitter",
            "Twitter handle",
            null,
            TwitterMaxLength,
            cancellationToken);

    public AccountFieldError? ValidateBio(string? bio) =>
        bio is not null && Encoding.UTF8.GetByteCount(bio) > BioMaxBytes
            ? new AccountFieldError("Bio", $"Bio must not be more than {BioMaxBytes} bytes long.")
            : null;

    public AccountFieldError? ValidateLore(string? lore) =>
        lore is not null && lore.Length > LoreMaxLength
            ? new AccountFieldError("Lore", $"Lore must not be more than {LoreMaxLength} characters long.")
            : null;

    private async Task<AccountFieldError?> ValidateSocialAsync(
        int exceptUserId,
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
            return new AccountFieldError(field, lengthError);
        }

        return await accountRepository.SocialExistsAsync(exceptUserId, column, value, cancellationToken)
            ? new AccountFieldError(field, $"{label} is already linked by another user.")
            : null;
    }

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

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[0-9a-z_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordRegex();

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordIdRegex();
}
