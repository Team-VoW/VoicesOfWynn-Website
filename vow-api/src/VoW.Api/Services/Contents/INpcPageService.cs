using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Contents;

public interface INpcPageService
{
    Task<NpcListResponse> ListAsync(NpcSearchRequest request, CancellationToken cancellationToken);

    Task<NpcDetailResponse?> GetAsync(int npcId, CancellationToken cancellationToken);

    Task<MyNpcVoteResponse?> GetVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);

    /// <summary>
    /// The caller's standing votes among a set of NPCs, for a page that lists several of them.
    /// </summary>
    Task<NpcVotesResponse> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);
}
