using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureNpcImageStorage : INpcImageStorage
{
    private const string ContainerName = "vow-dynamic";
    private const string ImageKeyPrefix = "npcs/";
    private const string ImageContentType = "image/webp";
    private const string ImageCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureNpcImageStorage(BlobServiceClient blobServiceClient)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(npcId));
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = ImageContentType,
                CacheControl = ImageCacheControl,
            },
        };
        await blob.UploadAsync(webpContent, options, cancellationToken);
    }

    private static string BlobKey(int npcId) =>
        $"{ImageKeyPrefix}{npcId.ToString(CultureInfo.InvariantCulture)}.webp";
}
