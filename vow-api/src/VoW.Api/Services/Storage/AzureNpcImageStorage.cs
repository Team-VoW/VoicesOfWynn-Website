using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureNpcImageStorage : INpcImageStorage
{
    private const string ImageKeyPrefix = "npcs/";
    private const string ImageContentType = "image/webp";
    private const string ImageCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureNpcImageStorage(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(StorageConfiguration.GetContainerName(configuration));
    }

    public async Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(npcId));
        await blob.UploadAsync(webpContent, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = ImageContentType,
            CacheControl = ImageCacheControl,
        }, cancellationToken: cancellationToken);
    }

    private static string BlobKey(int npcId) =>
        $"{ImageKeyPrefix}{npcId.ToString(CultureInfo.InvariantCulture)}.webp";
}
