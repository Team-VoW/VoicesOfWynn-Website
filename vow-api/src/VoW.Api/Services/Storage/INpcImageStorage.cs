namespace VoW.Api.Services.Storage;

/// <summary>
/// Stores normalized NPC image assets.
/// </summary>
public interface INpcImageStorage
{
    /// <summary>
    /// Uploads WebP image content for an existing NPC.
    /// </summary>
    /// <param name="npcId">The valid NPC identifier used as the storage key.</param>
    /// <param name="webpContent">
    /// WebP image content to upload. The caller retains ownership of the stream and must dispose it.
    /// The stream is expected to contain already validated WebP data within the API's accepted image size limit.
    /// It does not need to be seekable.
    /// </param>
    /// <param name="cancellationToken">Stops the upload when cancellation is requested.</param>
    /// <exception cref="ArgumentException">May be thrown when <paramref name="npcId"/> or content is invalid.</exception>
    /// <exception cref="Azure.RequestFailedException">Thrown when the storage provider rejects or fails the upload.</exception>
    Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the stored WebP image from one NPC key to another when the source image exists.
    /// </summary>
    /// <returns>Whether a source image existed and was copied.</returns>
    Task<bool> CopyImageIfExistsAsync(int sourceNpcId, int destinationNpcId, CancellationToken cancellationToken);
}
