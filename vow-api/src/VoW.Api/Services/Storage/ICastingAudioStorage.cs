namespace VoW.Api.Services.Storage;

/// <summary>Audition clips. Unlike NPC recordings they are private until cast, so reads go through short-lived links.</summary>
public interface ICastingAudioStorage
{
    Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken);

    Uri GetReadUrl(string blobPath);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken);
}
