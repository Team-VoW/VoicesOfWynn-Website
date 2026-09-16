using System.Security.Claims;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Domain.Npcs;

namespace VoW.Api.Services.Npcs;

public interface INpcVoteService
{
    Task<NpcWriteResult<NpcVoteResponse>> SetVoteAsync(
        int npcId,
        VoteType vote,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);

    Task<NpcWriteResult<NpcVoteResponse>> ClearVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);

    /// <summary>
    /// The caller's standing votes across a contributor's NPCs, so the cast page can highlight them
    /// without making the cacheable profile response vary per visitor.
    /// </summary>
    Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);
}
