namespace VoW.Api.Contracts.Npcs;

public sealed record NpcRecordingsResponse(IReadOnlyCollection<NpcQuestRecordingsResponse> Quests);

public sealed record NpcQuestRecordingsResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    IReadOnlyCollection<NpcRecordingLineResponse> Recordings);

public sealed record NpcRecordingLineResponse(int RecordingId, short Line, string Url);
