using System.ComponentModel.DataAnnotations;
using VoW.Api.Domain.Casting;

namespace VoW.Api.Contracts.Casting;

// Everything a voter receives. None of these carry another voter's identity or any vote totals:
// comments from others arrive as bare strings, and only once the voter has marked the character done.

public sealed record CastingRoundSummaryResponse(
    int Id,
    string Name,
    string? Description,
    DateTime? VotingClosesAt,
    bool VotingOpen,
    int CharacterCount,
    int DoneCount,
    int PickCount);

public sealed record CastingRoundListResponse(IReadOnlyList<CastingRoundSummaryResponse> Rounds);

public sealed record CastingCharacterSummaryResponse(
    int Id,
    string Name,
    string? QuestName,
    string? Direction,
    int AuditionCount,
    int MyPickCount,
    bool Done);

public sealed record CastingRoundDetailResponse(
    int Id,
    string Name,
    string? Description,
    DateTime? VotingClosesAt,
    bool VotingOpen,
    IReadOnlyList<CastingCharacterSummaryResponse> Characters);

public sealed record CastingAuditionResponse(
    int Id,
    int Number,
    string AuditioneeName,
    string AudioUrl,
    double? DurationSeconds,
    bool MyVote,
    string? MyComment,
    IReadOnlyList<string> AnonymousComments);

public sealed record CastingAuditionListResponse(
    int CharacterId,
    bool CommentsRevealed,
    IReadOnlyList<CastingAuditionResponse> Auditions);

public sealed record CastingMyPickResponse(
    int CharacterId,
    int AuditionId,
    int Number,
    string AuditioneeName,
    string AudioUrl,
    bool Picked,
    string? Comment);

/// <summary>Everything the voter picked or commented on; <c>Picked</c> tells the two apart.</summary>
public sealed record CastingMyPicksResponse(IReadOnlyList<CastingMyPickResponse> Picks);

public sealed class SetCommentRequest
{
    [StringLength(1000)]
    public string? Comment { get; init; }
}

public sealed class CastVoteRequest
{
    [StringLength(1000)]
    public string? Comment { get; init; }
}
