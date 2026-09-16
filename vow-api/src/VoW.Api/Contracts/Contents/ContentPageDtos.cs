namespace VoW.Api.Contracts.Contents;

/// <summary>A contributor credited on a quest or NPC page.</summary>
public sealed record ContentCreditResponse(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl);

public sealed record QuestListItemResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    int NpcCount,
    int RecordingCount);

public sealed record QuestListResponse(IReadOnlyCollection<QuestListItemResponse> Quests);

public sealed record QuestNpcResponse(
    int NpcId,
    string NpcName,
    string ImageUrl,
    string DefaultImageUrl,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    ContentCreditResponse? VoiceActor,
    ContentCreditResponse? SoundEditor);

public sealed record QuestDetailResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    string? ScriptUrl,
    ContentCreditResponse? Writer,
    IReadOnlyCollection<QuestNpcResponse> Npcs);

public sealed record NpcQuestCreditResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    ContentCreditResponse? SoundEditor);

public sealed record NpcDetailResponse(
    int NpcId,
    string NpcName,
    string ImageUrl,
    string DefaultImageUrl,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    ContentCreditResponse? VoiceActor,
    IReadOnlyCollection<NpcQuestCreditResponse> Quests);
