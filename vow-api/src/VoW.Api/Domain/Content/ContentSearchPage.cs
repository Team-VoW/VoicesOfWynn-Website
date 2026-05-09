namespace VoW.Api.Domain.Content;

public sealed record ContentSearchPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<QuestContentSummary> Results);

public sealed record QuestContentSummary(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    int? WriterId,
    string? WriterName,
    IReadOnlyCollection<NpcContentSummary> Npcs);

public sealed record NpcContentSummary(
    int NpcId,
    string NpcName,
    string NpcDegeneratedName,
    int? VoiceActorId,
    string? VoiceActorName,
    int? SoundEditorId,
    string? SoundEditorName,
    int RecordingCount);
