using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySqlConnector;
using VoW.Api.Contracts.DiscordIntegration;
using VoW.Api.Domain.Accounts;
using VoW.Api.Domain.DiscordIntegration;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.DiscordIntegration;

public sealed partial class DiscordIntegrationService(
    IDiscordIntegrationRepository repository,
    IAccountAvatarStorage avatarStorage,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DiscordIntegrationService> logger) : IDiscordIntegrationService
{
    private const int DisplayNameMinLength = 3;
    private const int DisplayNameMaxLength = 31;
    private const int DiscordNameMinLength = 2;
    private const int DiscordNameMaxLength = 37;
    private const int DiscordIdMaxLength = 19;
    private const int AvatarUrlMaxLength = 2048;
    private const int AvatarMaxBytes = 8_000_000;
    private const string PngImageContentType = "image/png";
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private readonly string storageBaseUrl = NormalizeStorageBaseUrl(
        StorageConfiguration.GetBaseUrl(configuration));

    public async Task<IReadOnlyCollection<DiscordIntegrationUserResponse>> GetUsersAsync(
        CancellationToken cancellationToken)
    {
        var users = await repository.GetUsersAsync(cancellationToken);
        return users.Select(user => new DiscordIntegrationUserResponse(
            user.UserId,
            user.DisplayName,
            user.DiscordId,
            user.DiscordName,
            AvatarUrl(user.Picture, user.PictureType),
            DefaultAvatarUrl(),
            user.RoleNames)).ToArray();
    }

    public async Task<SyncDiscordUserServiceResult> SyncUserAsync(
        SyncDiscordUserRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string>();
        var discordId = NormalizeOptional(request.DiscordId);
        var discordIdError = ValidateDiscordId(discordId);
        if (discordIdError is not null)
        {
            errors[nameof(request.DiscordId)] = discordIdError;
        }

        var discordName = NormalizeDiscordName(request.DiscordName);
        var discordNameError = ValidateDiscordName(discordName);
        if (discordNameError is not null)
        {
            errors[nameof(request.DiscordName)] = discordNameError;
        }

        var displayName = NormalizeOptional(request.DisplayName);
        if (displayName is not null)
        {
            var displayNameError = ValidateLength(displayName, "Display name", DisplayNameMinLength, DisplayNameMaxLength);
            if (displayNameError is not null)
            {
                errors[nameof(request.DisplayName)] = displayNameError;
            }
        }

        var avatarUrl = NormalizeOptional(request.AvatarUrl);
        if (avatarUrl is not null && !IsAllowedAvatarUrl(avatarUrl))
        {
            errors[nameof(request.AvatarUrl)] = "Avatar URL must be an absolute HTTP or HTTPS URL.";
        }

        if (errors.Count > 0)
        {
            return SyncDiscordUserServiceResult.Invalid(errors);
        }

        var roleIdsResult = await ResolveRoleIdsAsync(request.RoleNames, cancellationToken);
        if (!roleIdsResult.Succeeded)
        {
            return roleIdsResult.Result!;
        }

        var user = await repository.GetUserByDiscordIdAsync(discordId!, cancellationToken);
        var created = false;
        string? temporaryPassword = null;

        if (user is null)
        {
            user = await repository.GetUserByDiscordNameAsync(discordName!, cancellationToken);
        }

        if (user is null)
        {
            displayName ??= discordName;
            var displayNameConflict = await repository.DisplayNameExistsAsync(0, displayName!, cancellationToken);
            if (displayNameConflict)
            {
                return SyncDiscordUserServiceResult.Invalid(nameof(request.DisplayName), "This display name is already in use.");
            }

            temporaryPassword = GenerateTemporaryPassword();
            try
            {
                var userId = await repository.InsertUserAsync(
                    new CreateDiscordSyncUserCommand(
                        displayName!,
                        HashPassword(temporaryPassword),
                        discordId!,
                        discordName!),
                    cancellationToken);
                user = new DiscordSyncUser(userId, displayName!, discordId, discordName, "default.png", PictureType.Default);
                created = true;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return SyncDiscordUserServiceResult.Invalid(nameof(request.DiscordId), "Account creation conflicts with existing account data.");
            }
        }
        else
        {
            if (await repository.DiscordIdExistsAsync(user.UserId, discordId!, cancellationToken))
            {
                return SyncDiscordUserServiceResult.Invalid(nameof(request.DiscordId), "This Discord ID is already linked by another user.");
            }

            if (await repository.DiscordNameExistsAsync(user.UserId, discordName!, cancellationToken))
            {
                return SyncDiscordUserServiceResult.Invalid(nameof(request.DiscordName), "Discord username is already linked by another user.");
            }

            try
            {
                await repository.UpdateDiscordFieldsAsync(user.UserId, discordId!, discordName!, cancellationToken);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return SyncDiscordUserServiceResult.Invalid(nameof(request.DiscordId), "Account update conflicts with existing account data.");
            }
        }

        if (roleIdsResult.RoleIds is not null)
        {
            await repository.ReplaceRolesAsync(user.UserId, roleIdsResult.RoleIds, cancellationToken);
        }

        if (avatarUrl is not null && user.PictureType != PictureType.Manual)
        {
            try
            {
                var avatarResult = await UpdateDiscordAvatarAsync(user.UserId, avatarUrl, cancellationToken);
                if (!avatarResult.Succeeded)
                {
                    logger.LogWarning(
                        "Discord avatar update for user {UserId} was skipped: {Errors}.",
                        user.UserId,
                        string.Join("; ", avatarResult.Result!.Errors.Select(error => $"{error.Key}: {error.Value}")));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discord avatar update for user {UserId} failed.", user.UserId);
            }
        }

        return SyncDiscordUserServiceResult.Success(
            new SyncDiscordUserResponse(user.UserId, created, temporaryPassword));
    }

    private async Task<RoleIdsResolveResult> ResolveRoleIdsAsync(
        IReadOnlyCollection<string>? roleNames,
        CancellationToken cancellationToken)
    {
        if (roleNames is null)
        {
            return RoleIdsResolveResult.Success(null);
        }

        var normalized = roleNames
            .Select(NormalizeOptional)
            .Where(roleName => roleName is not null)
            .Select(roleName => roleName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rolesByName = await repository.GetRoleIdsByNameAsync(cancellationToken);
        var unknown = normalized
            .Where(roleName => !rolesByName.ContainsKey(roleName))
            .ToArray();
        if (unknown.Length > 0)
        {
            return RoleIdsResolveResult.Invalid(nameof(SyncDiscordUserRequest.RoleNames), $"Unknown role name: {unknown[0]}.");
        }

        return RoleIdsResolveResult.Success(normalized.Select(roleName => rolesByName[roleName]).ToArray());
    }

    private async Task<AvatarUpdateResult> UpdateDiscordAvatarAsync(
        int userId,
        string avatarUrl,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("VoicesOfWynn", "1.0"));

        using var response = await httpClient.GetAsync(
            avatarUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Discord avatar download for user {UserId} failed with status {StatusCode}.",
                userId,
                response.StatusCode);
            return AvatarUpdateResult.Invalid(nameof(SyncDiscordUserRequest.AvatarUrl), "Avatar URL could not be downloaded.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var limited = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > AvatarMaxBytes)
            {
                return AvatarUpdateResult.Invalid(nameof(SyncDiscordUserRequest.AvatarUrl), $"Avatar image must not exceed {AvatarMaxBytes} bytes.");
            }

            await limited.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        limited.Position = 0;
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!IsPng(contentType, limited))
        {
            return AvatarUpdateResult.Invalid(nameof(SyncDiscordUserRequest.AvatarUrl), "Avatar image must be PNG.");
        }

        limited.Position = 0;
        await avatarStorage.UploadDiscordAvatarAsync(userId, limited, cancellationToken);
        var picture = $"{userId.ToString(CultureInfo.InvariantCulture)}.png";
        if (!await repository.SetDiscordAvatarAsync(userId, picture, cancellationToken))
        {
            logger.LogWarning(
                "Uploaded Discord avatar for user {UserId}, but the database avatar update did not affect an account.",
                userId);
            return AvatarUpdateResult.Invalid(nameof(SyncDiscordUserRequest.AvatarUrl), "Avatar could not be linked to the user.");
        }

        return AvatarUpdateResult.Success();
    }

    private string AvatarUrl(string picture, PictureType pictureType) =>
        pictureType == PictureType.Default
            ? DefaultAvatarUrl()
            : $"{storageBaseUrl}avatars/{picture}";

    private string DefaultAvatarUrl() => $"{storageBaseUrl}avatars/default.png";

    private static bool IsAllowedAvatarUrl(string avatarUrl) =>
        avatarUrl.Length <= AvatarUrlMaxLength
        && Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NormalizeDiscordName(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized is not null && normalized.EndsWith("#0000", StringComparison.Ordinal)
            ? normalized[..^5].Trim()
            : normalized;
    }

    private static string? ValidateDiscordId(string? discordId)
    {
        if (discordId is null)
        {
            return "Discord ID is required.";
        }

        if (discordId.Length > DiscordIdMaxLength
            || !DiscordIdRegex().IsMatch(discordId)
            || !ulong.TryParse(discordId, out var parsedDiscordId)
            || parsedDiscordId == 0)
        {
            return "Discord ID must be a positive numeric Discord user ID.";
        }

        return null;
    }

    private static string? ValidateDiscordName(string? discordName)
    {
        var lengthError = ValidateLength(discordName, "Discord username", DiscordNameMinLength, DiscordNameMaxLength);
        if (lengthError is not null)
        {
            return lengthError;
        }

        return discordName is not null && (!DiscordNameRegex().IsMatch(discordName) || discordName.Contains("..", StringComparison.Ordinal))
            ? "Discord username is in incorrect format."
            : null;
    }

    private static string? ValidateLength(string? value, string label, int minLength, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return $"{label} is required.";
        }

        if (value.Length > maxLength)
        {
            return $"{label} must not be more than {maxLength} characters long.";
        }

        return value.Length < minLength
            ? $"{label} must not be less than {minLength} characters long."
            : null;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
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

    private static string NormalizeStorageBaseUrl(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";

    private static bool IsPng(string? contentType, Stream content)
    {
        if (contentType is not null && !string.Equals(contentType, PngImageContentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (content.Length < PngSignature.Length)
        {
            return false;
        }

        Span<byte> signature = stackalloc byte[PngSignature.Length];
        var originalPosition = content.Position;
        content.Position = 0;
        var read = content.Read(signature);
        content.Position = originalPosition;
        return read == PngSignature.Length && signature.SequenceEqual(PngSignature);
    }

    [GeneratedRegex(@"^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordIdRegex();

    [GeneratedRegex(@"^[0-9a-z_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DiscordNameRegex();

    private sealed record RoleIdsResolveResult(IReadOnlyCollection<int>? RoleIds, SyncDiscordUserServiceResult? Result)
    {
        public bool Succeeded => Result is null;

        public static RoleIdsResolveResult Success(IReadOnlyCollection<int>? roleIds) => new(roleIds, null);

        public static RoleIdsResolveResult Invalid(string field, string message) =>
            new(null, SyncDiscordUserServiceResult.Invalid(field, message));
    }

    private sealed record AvatarUpdateResult(SyncDiscordUserServiceResult? Result)
    {
        public bool Succeeded => Result is null;

        public static AvatarUpdateResult Success() => new((SyncDiscordUserServiceResult?)null);

        public static AvatarUpdateResult Invalid(string field, string message) =>
            new(SyncDiscordUserServiceResult.Invalid(field, message));
    }
}
