using System.ComponentModel.DataAnnotations;
using VoW.Api.Domain.Npcs;

namespace VoW.Api.Contracts.Npcs;

public sealed record NpcVoteRequest([Required] VoteType? Vote);

public sealed record NpcVoteResponse(int Upvotes, int Downvotes, VoteType? MyVote);

/// <summary>
/// The caller's standing votes among a set of NPCs. Kept apart from the pages that list those NPCs
/// so those responses stay identical for every visitor and can be cached.
/// </summary>
public sealed record NpcVotesResponse(
    IReadOnlyCollection<int> Upvoted,
    IReadOnlyCollection<int> Downvoted);

/// <summary>The caller's standing vote on a single NPC, split out for the same reason.</summary>
public sealed record MyNpcVoteResponse(VoteType? MyVote);
