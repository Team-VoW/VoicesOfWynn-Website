namespace VoW.Api.Contracts.Contributors;

public sealed record ContributorDetailResponse(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Lore,
    // Bio is sanitized HTML, not plain text: the client renders it as markup.
    string? Bio,
    string? Email,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    IReadOnlyCollection<ContributorRoleResponse> Roles,
    IReadOnlyCollection<VoicedNpcResponse> Voicing,
    IReadOnlyCollection<WrittenQuestResponse> Scriptwriting,
    IReadOnlyCollection<EditedQuestResponse> SoundEditing);

public sealed record NpcQuestAppearanceResponse(int QuestId, string QuestName, string QuestDegeneratedName);

public sealed record VoicedNpcResponse(
    int NpcId,
    string NpcName,
    string ImageUrl,
    string DefaultImageUrl,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    IReadOnlyCollection<NpcQuestAppearanceResponse> Quests);

public sealed record WrittenQuestResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    string? ScriptUrl);

public sealed record EditedNpcResponse(int NpcId, string NpcName);

public sealed record EditedQuestResponse(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    IReadOnlyCollection<EditedNpcResponse> Npcs);

/// <summary>
/// The caller's standing votes on a contributor's NPCs. Kept out of the profile response so that
/// stays identical for every visitor and can be cached.
/// </summary>
public sealed record ContributorVotesResponse(
    IReadOnlyCollection<int> Upvoted,
    IReadOnlyCollection<int> Downvoted);
