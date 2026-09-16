using System.Security.Claims;
using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Npcs;

public interface INpcCommentService
{
    Task<NpcCommentsResponse?> GetCommentsAsync(
        int npcId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);

    Task<NpcWriteResult<NpcCommentResponse>> PostAsync(
        int npcId,
        PostNpcCommentRequest request,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);

    Task<NpcWriteResult<bool>> DeleteAsync(
        int npcId,
        int commentId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
