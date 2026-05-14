using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureAccountAvatarStorage : IAccountAvatarStorage
{
    private const string AvatarKeyPrefix = "avatars/";
    private const string WebpImageContentType = "image/webp";
    private const string PngImageContentType = "image/png";
    private const string ImageCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureAccountAvatarStorage(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(StorageConfiguration.GetContainerName(configuration));
    }

    public async Task UploadAvatarAsync(int userId, Stream webpContent, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(WebpBlobKey(userId));
        await blob.UploadAsync(webpContent, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = WebpImageContentType,
            CacheControl = ImageCacheControl,
        }, cancellationToken: cancellationToken);
    }

    public async Task UploadDiscordAvatarAsync(int userId, Stream pngContent, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(PngBlobKey(userId));
        await blob.UploadAsync(pngContent, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = PngImageContentType,
            CacheControl = ImageCacheControl,
        }, cancellationToken: cancellationToken);
    }

    public async Task DeleteCustomAvatarsAsync(int userId, CancellationToken cancellationToken)
    {
        var prefix = $"{AvatarKeyPrefix}{userId.ToString(CultureInfo.InvariantCulture)}.";
        await foreach (var item in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            await containerClient.DeleteBlobIfExistsAsync(item.Name, cancellationToken: cancellationToken);
        }
    }

    private static string WebpBlobKey(int userId) =>
        $"{AvatarKeyPrefix}{userId.ToString(CultureInfo.InvariantCulture)}.webp";

    private static string PngBlobKey(int userId) =>
        $"{AvatarKeyPrefix}{userId.ToString(CultureInfo.InvariantCulture)}.png";
}
