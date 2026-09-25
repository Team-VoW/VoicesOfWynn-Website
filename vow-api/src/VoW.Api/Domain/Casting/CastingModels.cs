namespace VoW.Api.Domain.Casting;

/// <summary>
/// Draft rounds are only visible to casting managers, open rounds are what voters see, closed rounds
/// are read-only for review and picking winners, archived rounds drop out of the default lists.
/// </summary>
public enum CastingRoundStatus
{
    Draft,
    Open,
    Closed,
    Archived
}

public enum CastingSource
{
    Manual,
    Ccc,
    Discord
}

public enum CastingImportStatus
{
    Idle,
    Running,
    Done,
    Failed
}

public sealed record CastingRound(
    int Id,
    string Name,
    string? Description,
    CastingRoundStatus Status,
    CastingSource Source,
    string? SourceRef,
    DateTime? VotingClosesAt,
    CastingImportStatus ImportStatus,
    string? ImportMessage,
    int? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>A round past its closing time stops taking votes without anyone flipping its status.</summary>
    public bool IsVotingOpen(DateTime nowUtc) =>
        Status == CastingRoundStatus.Open && (VotingClosesAt is null || VotingClosesAt > nowUtc);
}

public sealed record CastingRoundDetails(
    string Name,
    string? Description,
    DateTime? VotingClosesAt);

public sealed record NewCastingRound(
    CastingRoundDetails Details,
    CastingSource Source,
    string? SourceRef,
    int? CreatedBy);

public sealed record CastingCharacter(
    int Id,
    int RoundId,
    string Name,
    string? QuestName,
    string? Direction,
    int SortOrder,
    int? WinnerAuditionId);

public sealed record CastingCharacterDetails(string Name, string? QuestName, string? Direction);

public sealed record CastingAudition(
    int Id,
    int CharacterId,
    int Number,
    string AuditioneeName,
    int? AuditioneeUserId,
    string? SourceRef,
    string AudioBlobPath,
    double? DurationSeconds,
    DateTime CreatedAt);

public sealed record NewCastingAudition(
    int CharacterId,
    string AuditioneeName,
    int? AuditioneeUserId,
    string? SourceRef,
    string AudioBlobPath,
    double? DurationSeconds);

/// <summary>
/// A voter's take on one audition: a pick, a comment, or both. Only the review may turn
/// <see cref="UserId"/> into a name.
/// </summary>
public sealed record CastingVote(int AuditionId, int CharacterId, int UserId, bool Picked, string? Comment);

public sealed record CastingDone(int CharacterId, int UserId);

public sealed record CastingVoter(int UserId, string DisplayName);
