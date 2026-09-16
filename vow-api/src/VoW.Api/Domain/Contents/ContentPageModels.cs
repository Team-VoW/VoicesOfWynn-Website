using VoW.Api.Domain.Contributors;

namespace VoW.Api.Domain.Contents;

/// <summary>One quest in the mod-contents index.</summary>
public sealed record QuestListItem(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    int NpcCount,
    int RecordingCount);

/// <summary>A contributor credited on a quest or NPC page, with only what a link and avatar need.</summary>
public sealed record ContentCredit(int UserId, string DisplayName, string AvatarUrl, string DefaultAvatarUrl);

/// <summary>One NPC in a quest's cast. Recordings load separately, per NPC.</summary>
public sealed record QuestNpc(
    int NpcId,
    string NpcName,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    ContentCredit? VoiceActor,
    ContentCredit? SoundEditor);

public sealed record QuestDetail(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    ContentCredit? Writer,
    IReadOnlyCollection<QuestNpc> Npcs);

/// <summary>A quest an NPC speaks in, with the editor credited for that quest's lines.</summary>
public sealed record NpcQuestCredit(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    ContentCredit? SoundEditor);

public sealed record NpcDetail(
    int NpcId,
    string NpcName,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    ContentCredit? VoiceActor,
    IReadOnlyCollection<NpcQuestCredit> Quests);

/// <summary>What the NPC index is narrowed by. A null search asks for every NPC.</summary>
public sealed record NpcListCriteria(string? Search, int Page, int PageSize);

/// <summary>
/// One NPC in the mod-contents index. Carries the same counts a quest's cast does, plus the
/// quests it speaks in - the index is not about any one quest, so the card has to say where
/// each character comes from.
/// </summary>
public sealed record NpcListItem(
    int NpcId,
    string NpcName,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    ContentCredit? VoiceActor,
    IReadOnlyCollection<NpcQuestAppearance> Quests);

/// <summary>One page of the NPC index. Unlike the quest index this is paged, there being far more NPCs.</summary>
public sealed record NpcListPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<NpcListItem> Results);
