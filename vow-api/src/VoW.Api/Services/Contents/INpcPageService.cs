using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Contents;

public interface INpcPageService
{
    Task<NpcDetailResponse?> GetAsync(int npcId, CancellationToken cancellationToken);

    Task<MyNpcVoteResponse?> GetVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);
}
