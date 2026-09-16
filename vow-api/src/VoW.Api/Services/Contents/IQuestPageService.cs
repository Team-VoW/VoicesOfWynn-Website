using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;

namespace VoW.Api.Services.Contents;

public interface IQuestPageService
{
    Task<QuestListResponse> ListAsync(CancellationToken cancellationToken);

    /// <summary>The quest addressed by its URL name, or null when no such quest exists.</summary>
    Task<QuestDetailResponse?> GetAsync(string degeneratedName, CancellationToken cancellationToken);

    Task<NpcVotesResponse?> GetVotesAsync(
        string degeneratedName,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken);
}
