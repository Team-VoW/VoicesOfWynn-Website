using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureAccountAvatarStorage : IAccountAvatarStorage
{
    private const string ContainerName = "vow-dynamic";
    private const string AvatarKeyPrefix = "avatars/";
    private const string ImageContentType = "image/webp";
    private const string ImageCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureAccountAvatarStorage(BlobServiceClient blobServiceClient)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task UploadAvatarAsync(int userId, Stream webpContent, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(userId));
        await blob.UploadAsync(webpContent, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = ImageContentType,
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

    private static string BlobKey(int userId) =>
        $"{AvatarKeyPrefix}{userId.ToString(CultureInfo.InvariantCulture)}.webp";
}
