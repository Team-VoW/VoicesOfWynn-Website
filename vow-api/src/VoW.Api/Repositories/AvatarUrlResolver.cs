using VoW.Api.Domain.Accounts;
using VoW.Api.Services.Storage;

namespace VoW.Api.Repositories;

/// <summary>
/// Turns the <c>user.picture</c> / <c>user.picture_type</c> column pair into a public avatar URL.
/// Shared by every repository that surfaces a user, so the storage layout lives in one place.
/// </summary>
public sealed class AvatarUrlResolver
{
    private readonly string storageBaseUrl;

    public AvatarUrlResolver(IConfiguration configuration)
        : this(StorageConfiguration.GetBaseUrl(configuration))
    {
    }

    public AvatarUrlResolver(string storageBaseUrl) =>
        this.storageBaseUrl = storageBaseUrl.EndsWith('/') ? storageBaseUrl : $"{storageBaseUrl}/";

    public string AvatarUrl(string picture, PictureType pictureType) =>
        pictureType == PictureType.Default ? DefaultAvatarUrl() : $"{storageBaseUrl}avatars/{picture}";

    /// <summary>Resolves straight from the raw <c>picture_type</c> column value.</summary>
    public string AvatarUrl(string picture, string pictureType) =>
        AvatarUrl(picture, ParsePictureType(pictureType));

    public string DefaultAvatarUrl() => $"{storageBaseUrl}avatars/default.png";

    public static PictureType ParsePictureType(string value) =>
        value switch
        {
            "default" => PictureType.Default,
            "discord" => PictureType.Discord,
            "manual" => PictureType.Manual,
            _ => throw new InvalidOperationException($"Unknown picture type '{value}'.")
        };

    public static string ToDatabaseValue(PictureType pictureType) =>
        pictureType switch
        {
            PictureType.Default => "default",
            PictureType.Discord => "discord",
            PictureType.Manual => "manual",
            _ => throw new ArgumentOutOfRangeException(nameof(pictureType), pictureType, "Unknown picture type.")
        };
}
