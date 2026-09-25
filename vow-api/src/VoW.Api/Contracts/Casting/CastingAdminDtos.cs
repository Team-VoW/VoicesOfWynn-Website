using System.ComponentModel.DataAnnotations;
using VoW.Api.Domain.Casting;

namespace VoW.Api.Contracts.Casting;

public sealed record AdminCastingRoundResponse(
    int Id,
    string Name,
    string? Description,
    CastingRoundStatus Status,
    CastingSource Source,
    string? SourceRef,
    DateTime? VotingClosesAt,
    bool VotingOpen,
    CastingImportStatus ImportStatus,
    string? ImportMessage,
    int CharacterCount,
    int AuditionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AdminCastingRoundListResponse(IReadOnlyList<AdminCastingRoundResponse> Rounds);

public sealed record AdminCastingAuditionResponse(
    int Id,
    int Number,
    string AuditioneeName,
    int? AuditioneeUserId,
    string AudioUrl,
    double? DurationSeconds);

public sealed record AdminCastingCharacterResponse(
    int Id,
    string Name,
    string? QuestName,
    string? Direction,
    int? WinnerAuditionId,
    IReadOnlyList<AdminCastingAuditionResponse> Auditions);

public sealed record AdminCastingRoundDetailResponse(
    AdminCastingRoundResponse Round,
    IReadOnlyList<AdminCastingCharacterResponse> Characters);

public sealed class SaveCastingRoundRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>Interpreted as UTC.</summary>
    public DateTime? VotingClosesAt { get; init; }
}

public sealed class SetCastingRoundStatusRequest
{
    [Required]
    public CastingRoundStatus? Status { get; init; }
}

public sealed class SaveCastingCharacterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(100)]
    public string? QuestName { get; init; }

    [StringLength(2000)]
    public string? Direction { get; init; }
}

public sealed class UploadCastingAuditionRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AuditioneeName { get; init; } = string.Empty;

    [Required]
    public IFormFile? File { get; init; }
}

public sealed class ImportCccRequest
{
    [Required]
    [StringLength(500)]
    public string Url { get; init; } = string.Empty;
}

public sealed class SetCastingWinnerRequest
{
    /// <summary>Null clears the winner.</summary>
    public int? AuditionId { get; init; }
}

/// <summary>A voter's pick and/or comment. <c>Picked</c> is false for a comment without a vote.</summary>
public sealed record CastingReviewVoteResponse(string VoterName, bool Picked, string? Comment);

public sealed record CastingReviewAuditionResponse(
    int Id,
    int Number,
    string AuditioneeName,
    string AudioUrl,
    double? DurationSeconds,
    int VoteCount,
    IReadOnlyList<CastingReviewVoteResponse> Votes);

public sealed record CastingReviewCharacterResponse(
    int Id,
    string Name,
    string? QuestName,
    int AuditionCount,
    int TotalVotes,
    int? WinnerAuditionId,
    IReadOnlyList<string> DoneVoters,
    IReadOnlyList<string> AbstainedVoters,
    IReadOnlyList<string> PendingVoters,
    IReadOnlyList<CastingReviewAuditionResponse> Auditions);

public sealed record CastingReviewResponse(
    int RoundId,
    string RoundName,
    CastingRoundStatus Status,
    int EligibleVoterCount,
    IReadOnlyList<CastingReviewCharacterResponse> Characters);
