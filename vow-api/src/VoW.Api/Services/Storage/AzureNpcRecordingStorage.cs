using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureNpcRecordingStorage : INpcRecordingStorage
{
    private const string ContainerName = "vow-dynamic";
    private const string RecordingKeyPrefix = "recordings/";
    private const string RecordingContentType = "audio/ogg";
    private const string RecordingCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureNpcRecordingStorage(BlobServiceClient blobServiceClient)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
    }

    public Uri GetRecordingUrl(string fileName)
    {
        var blob = containerClient.GetBlobClient(BlobKey(fileName));
        return blob.Uri;
    }

    public async Task<bool> RecordingExistsAsync(string fileName, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(fileName));
        var response = await blob.ExistsAsync(cancellationToken);
        return response.Value;
    }

    public async Task UploadRecordingAsync(string fileName, Stream content, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(fileName));
        await blob.UploadAsync(content, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = RecordingContentType,
            CacheControl = RecordingCacheControl,
        }, cancellationToken: cancellationToken);
    }

    public async Task RenameRecordingAsync(
        string currentFileName,
        string newFileName,
        CancellationToken cancellationToken)
    {
        var source = containerClient.GetBlobClient(BlobKey(currentFileName));
        var destination = containerClient.GetBlobClient(BlobKey(newFileName));

        await destination.SyncCopyFromUriAsync(source.Uri, cancellationToken: cancellationToken);
        await destination.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = RecordingContentType,
            CacheControl = RecordingCacheControl,
        }, cancellationToken: cancellationToken);
        await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteRecordingAsync(string fileName, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(fileName));
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private static string BlobKey(string fileName) => $"{RecordingKeyPrefix}{fileName}";
}
