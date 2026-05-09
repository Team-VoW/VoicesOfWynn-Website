namespace VoW.Api.Contracts.Content;

public sealed record ContentSearchResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<QuestContentResult> Results);

public sealed record QuestContentResult(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    IReadOnlyCollection<NpcContentResult> Npcs);

public sealed record NpcContentResult(
    int NpcId,
    string NpcName,
    string NpcDegeneratedName,
    int? VoiceActorId,
    string? VoiceActorName,
    int RecordingCount);
