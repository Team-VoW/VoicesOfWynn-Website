using System.ComponentModel.DataAnnotations;
using VoW.Api.Domain.Npcs;

namespace VoW.Api.Contracts.Npcs;

public sealed record NpcVoteRequest([Required] VoteType? Vote);

public sealed record NpcVoteResponse(int Upvotes, int Downvotes, VoteType? MyVote);
