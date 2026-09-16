using VoW.Api.Contracts.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Npcs;

public sealed class NpcRecordingCatalogService(
    INpcInteractionRepository repository,
    INpcRecordingStorage recordingStorage) : INpcRecordingCatalogService
{
    public async Task<NpcRecordingsResponse?> GetRecordingsAsync(int npcId, CancellationToken cancellationToken)
    {
        if (!await repository.NpcExistsAsync(npcId, cancellationToken))
        {
            return null;
        }

        var recordings = await repository.GetRecordingsAsync(npcId, cancellationToken);
        if (recordings.Count == 0)
        {
            return new NpcRecordingsResponse([]);
        }

        var questNames = await repository.GetQuestNamesAsync(
            recordings.Select(recording => recording.QuestId).Distinct().ToArray(),
            cancellationToken);

        var quests = recordings
            .GroupBy(recording => recording.QuestId)
            .Select(group =>
            {
                var quest = questNames.GetValueOrDefault(group.Key);
                return new NpcQuestRecordingsResponse(
                    group.Key,
                    quest?.Name ?? string.Empty,
                    quest?.DegeneratedName ?? string.Empty,
                    group.Select(recording => new NpcRecordingLineResponse(
                        recording.RecordingId,
                        recording.Line,
                        recordingStorage.GetRecordingUrl(recording.FileName).ToString())).ToArray());
            })
            .ToArray();

        return new NpcRecordingsResponse(quests);
    }
}
