namespace VoW.Api.Services.Storage;

public interface INpcImageStorage
{
    Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken);
}
