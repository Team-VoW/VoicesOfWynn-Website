using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace VoW.Api.Services.Storage;

public sealed class AzureCastingAudioStorage : ICastingAudioStorage
{
    private const string DefaultContainerName = "casting-audio";
    private const string AudioContentType = "audio/mpeg";

    /// <summary>Long enough to listen through a character's auditions without the page going stale.</summary>
    private static readonly TimeSpan ReadLinkLifetime = TimeSpan.FromHours(6);

    private readonly BlobContainerClient containerClient;
    private readonly SemaphoreSlim containerGate = new(1, 1);
    private bool containerReady;

    public AzureCastingAudioStorage(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        var containerName = configuration["Storage:CastingContainerName"];
        containerClient = blobServiceClient.GetBlobContainerClient(
            string.IsNullOrWhiteSpace(containerName) ? DefaultContainerName : containerName);
    }

    public async Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);
        var blob = containerClient.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = AudioContentType, CacheControl = "private, max-age=3600" }
        }, cancellationToken);
    }

    public Uri GetReadUrl(string blobPath)
    {
        var blob = containerClient.GetBlobClient(blobPath);
        if (!blob.CanGenerateSasUri)
        {
            // Only reachable with token credentials; the connection string used everywhere carries a key.
            return blob.Uri;
        }

        // Rounded so every request in the same window gets the same link and the browser cache holds.
        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        return blob.GenerateSasUri(BlobSasPermissions.Read, windowStart + ReadLinkLifetime + TimeSpan.FromHours(1));
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken)
    {
        var blob = containerClient.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (containerReady)
        {
            return;
        }

        await containerGate.WaitAsync(cancellationToken);
        try
        {
            if (!containerReady)
            {
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
                containerReady = true;
            }
        }
        finally
        {
            containerGate.Release();
        }
    }
}
