namespace VoW.Api.Domain.Contributors;

/// <summary>A Discord role as shown on the credits and cast pages.</summary>
public sealed record ContributorRole(int Id, string Name, string Color, int Weight);

/// <summary>One card on the credits page.</summary>
public sealed record ContributorSummary(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Lore,
    ContributorRole TopRole,
    IReadOnlyCollection<ContributorRole> Roles);

public sealed record ContributorPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<ContributorSummary> Results);

/// <summary>Everything the cast page shows above the credit lists.</summary>
public sealed record ContributorProfile(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Lore,
    string? Bio,
    string? Email,
    bool PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    IReadOnlyCollection<ContributorRole> Roles);

/// <summary>A quest an NPC is voiced in. Recordings load separately, per NPC.</summary>
public sealed record NpcQuestAppearance(int QuestId, string QuestName, string QuestDegeneratedName);

public sealed record VoicedNpc(
    int NpcId,
    string NpcName,
    bool Archived,
    int Upvotes,
    int Downvotes,
    int CommentCount,
    int RecordingCount,
    IReadOnlyCollection<NpcQuestAppearance> Quests);

public sealed record WrittenQuest(int QuestId, string QuestName, string QuestDegeneratedName);

/// <summary>An NPC credited to a sound editor, grouped under the quest it was edited for.</summary>
public sealed record EditedNpc(int NpcId, string NpcName);

public sealed record EditedQuest(
    int QuestId,
    string QuestName,
    string QuestDegeneratedName,
    IReadOnlyCollection<EditedNpc> Npcs);

public sealed record ContributorCredits(
    ContributorProfile Profile,
    IReadOnlyCollection<VoicedNpc> Voicing,
    IReadOnlyCollection<WrittenQuest> Scriptwriting,
    IReadOnlyCollection<EditedQuest> SoundEditing);
