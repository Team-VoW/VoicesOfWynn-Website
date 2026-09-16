using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Controllers;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Npcs;

public sealed class NpcVoteService(
    INpcInteractionRepository repository,
    IWriteLimitRepository writeLimits) : INpcVoteService
{
    private const int VotesPerHour = 120;

    public async Task<NpcWriteResult<NpcVoteResponse>> SetVoteAsync(
        int npcId,
        VoteType vote,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var voterId = VoterId(user, ipAddress);
        var guard = await GuardAsync(npcId, voterId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var counts = await repository.SetVoteAsync(npcId, voterId, vote, cancellationToken);
        return NpcWriteResult<NpcVoteResponse>.Success(
            new NpcVoteResponse(counts.Upvotes, counts.Downvotes, vote));
    }

    public async Task<NpcWriteResult<NpcVoteResponse>> ClearVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var voterId = VoterId(user, ipAddress);
        var guard = await GuardAsync(npcId, voterId, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }

        var counts = await repository.ClearVoteAsync(npcId, voterId, cancellationToken);
        return NpcWriteResult<NpcVoteResponse>.Success(
            new NpcVoteResponse(counts.Upvotes, counts.Downvotes, null));
    }

    public async Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (npcIds.Count == 0)
        {
            return new Dictionary<int, VoteType>();
        }

        return await repository.GetVotesAsync(npcIds, VoterId(user, ipAddress), cancellationToken);
    }

    private async Task<NpcWriteResult<NpcVoteResponse>?> GuardAsync(
        int npcId,
        string voterId,
        CancellationToken cancellationToken)
    {
        if (!await repository.NpcExistsAsync(npcId, cancellationToken))
        {
            return NpcWriteResult<NpcVoteResponse>.NotFound();
        }

        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"npc-vote:{voterId}"));
        return await writeLimits.ConsumeLimitAsync(key, VotesPerHour, cancellationToken)
            ? null
            : NpcWriteResult<NpcVoteResponse>.RateLimited();
    }

    private static string VoterId(ClaimsPrincipal user, string ipAddress)
    {
        var userId = user.GetUserId();
        return userId is not null ? VoterIdentity.ForUser(userId.Value) : VoterIdentity.ForAddress(ipAddress);
    }
}
