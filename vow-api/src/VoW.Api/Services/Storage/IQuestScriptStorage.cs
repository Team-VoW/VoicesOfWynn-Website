namespace VoW.Api.Services.Storage;

/// <summary>
/// Stores and resolves quest script assets by their normalized storage name.
/// </summary>
public interface IQuestScriptStorage
{
    /// <summary>
    /// Uploads script content for the given normalized quest name.
    /// </summary>
    /// <param name="degeneratedName">A sanitized quest identifier used as the script storage key.</param>
    /// <param name="content">
    /// Script content to upload. The caller retains ownership of the stream and must dispose it.
    /// The implementation may read it asynchronously and does not require it to be seekable.
    /// </param>
    /// <param name="cancellationToken">Stops the upload when cancellation is requested.</param>
    /// <exception cref="Azure.RequestFailedException">Thrown when the storage provider rejects or fails the upload.</exception>
    Task UploadScriptAsync(string degeneratedName, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Returns true when a script exists for the given normalized quest name.
    /// </summary>
    /// <param name="degeneratedName">A sanitized quest identifier used as the script storage key.</param>
    /// <param name="cancellationToken">Stops the existence check when cancellation is requested.</param>
    Task<bool> ScriptExistsAsync(string degeneratedName, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the public URI for the script identified by the normalized quest name.
    /// </summary>
    /// <param name="degeneratedName">A sanitized quest identifier used as the script storage key.</param>
    Uri GetScriptUrl(string degeneratedName);
}
