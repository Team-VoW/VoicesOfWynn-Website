using System.Globalization;
using Azure;
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

    public async Task<bool> CopyImageIfExistsAsync(
        int sourceNpcId,
        int destinationNpcId,
        CancellationToken cancellationToken)
    {
        var source = containerClient.GetBlobClient(BlobKey(sourceNpcId));
        var destination = containerClient.GetBlobClient(BlobKey(destinationNpcId));
        Response<BlobDownloadStreamingResult> download;
        try
        {
            download = await source.DownloadStreamingAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }

        await using (download.Value.Content)
        {
            await destination.UploadAsync(download.Value.Content, overwrite: true, cancellationToken);
        }

        await destination.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = ImageContentType,
            CacheControl = ImageCacheControl,
        }, cancellationToken: cancellationToken);
        return true;
    }

    private static string BlobKey(int npcId) =>
        $"{ImageKeyPrefix}{npcId.ToString(CultureInfo.InvariantCulture)}.webp";
}
