using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace VoW.Api.Services.Storage;

public sealed class AzureQuestScriptStorage : IQuestScriptStorage
{
    private const string ScriptKeyPrefix = "scripts/";
    private const string ScriptContentType = "text/plain";
    private const string ScriptCacheControl = "public, max-age=3600";

    private readonly BlobContainerClient containerClient;

    public AzureQuestScriptStorage(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        containerClient = blobServiceClient.GetBlobContainerClient(StorageConfiguration.GetContainerName(configuration));
    }

    public async Task UploadScriptAsync(string degeneratedName, Stream content, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(degeneratedName));
        await blob.UploadAsync(content, overwrite: true, cancellationToken);
        await blob.SetHttpHeadersAsync(new BlobHttpHeaders
        {
            ContentType = ScriptContentType,
            CacheControl = ScriptCacheControl,
        }, cancellationToken: cancellationToken);
    }

    public async Task<bool> ScriptExistsAsync(string degeneratedName, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(BlobKey(degeneratedName));
        var response = await blob.ExistsAsync(cancellationToken);
        return response.Value;
    }

    public Uri GetScriptUrl(string degeneratedName) =>
        containerClient.GetBlobClient(BlobKey(degeneratedName)).Uri;

    private static string BlobKey(string degeneratedName) => $"{ScriptKeyPrefix}{degeneratedName}.txt";
}
