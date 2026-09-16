using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Npcs;

public interface INpcRecordingCatalogService
{
    Task<NpcRecordingsResponse?> GetRecordingsAsync(int npcId, CancellationToken cancellationToken);
}
