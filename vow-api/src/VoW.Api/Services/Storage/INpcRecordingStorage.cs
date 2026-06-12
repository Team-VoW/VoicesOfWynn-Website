namespace VoW.Api.Services.Storage;

public interface INpcRecordingStorage
{
    Uri GetRecordingUrl(string fileName);

    Task<bool> RecordingExistsAsync(string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads OGG recording content. The caller retains ownership of the stream and must dispose it.
    /// </summary>
    Task UploadRecordingAsync(string fileName, Stream content, CancellationToken cancellationToken);

    Task RenameRecordingAsync(string currentFileName, string newFileName, CancellationToken cancellationToken);

    Task<bool> TryRenameRecordingAsync(string currentFileName, string newFileName, CancellationToken cancellationToken);

    Task DeleteRecordingAsync(string fileName, CancellationToken cancellationToken);
}
