using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Contributors;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Domain.Contents;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Npcs;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Contents;

public sealed class NpcPageService(
    IContentPageRepository repository,
    INpcInteractionRepository npcRepository,
    INpcImageStorage npcImageStorage,
    INpcVoteService voteService) : INpcPageService
{
    /// <summary>The largest set of ids a caller may ask about at once, one generous scroll's worth.</summary>
    private const int MaxVoteLookup = 200;

    public async Task<NpcListResponse> ListAsync(NpcSearchRequest request, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var criteria = new NpcListCriteria(
            search,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100));

        var page = await repository.GetNpcListAsync(criteria, cancellationToken);
        var defaultImageUrl = npcImageStorage.GetDefaultImageUrl().ToString();

        return new NpcListResponse(
            page.Total,
            page.Page,
            page.PageSize,
            page.Results.Select(npc => new NpcListItemResponse(
                npc.NpcId,
                npc.NpcName,
                npcImageStorage.GetImageUrl(npc.NpcId).ToString(),
                defaultImageUrl,
                npc.Archived,
                npc.Upvotes,
                npc.Downvotes,
                npc.CommentCount,
                npc.RecordingCount,
                ContentCreditMapper.Credit(npc.VoiceActor),
                npc.Quests.Select(quest => new NpcQuestAppearanceResponse(
                    quest.QuestId,
                    quest.QuestName,
                    quest.QuestDegeneratedName)).ToArray())).ToArray());
    }

    public async Task<NpcDetailResponse?> GetAsync(int npcId, CancellationToken cancellationToken)
    {
        var npc = await repository.GetNpcAsync(npcId, cancellationToken);
        if (npc is null)
        {
            return null;
        }

        return new NpcDetailResponse(
            npc.NpcId,
            npc.NpcName,
            npcImageStorage.GetImageUrl(npc.NpcId).ToString(),
            npcImageStorage.GetDefaultImageUrl().ToString(),
            npc.Archived,
            npc.Upvotes,
            npc.Downvotes,
            npc.CommentCount,
            npc.RecordingCount,
            ContentCreditMapper.Credit(npc.VoiceActor),
            npc.Quests.Select(quest => new NpcQuestCreditResponse(
                quest.QuestId,
                quest.QuestName,
                quest.QuestDegeneratedName,
                ContentCreditMapper.Credit(quest.SoundEditor))).ToArray());
    }

    public async Task<MyNpcVoteResponse?> GetVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        if (!await npcRepository.NpcExistsAsync(npcId, cancellationToken))
        {
            return null;
        }

        var votes = await voteService.GetVotesAsync([npcId], user, ipAddress, cancellationToken);
        return new MyNpcVoteResponse(votes.TryGetValue(npcId, out var vote) ? vote : null);
    }

    public async Task<NpcVotesResponse> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        // Distinct and capped: the ids come straight off the query string, and the lookup builds
        // an IN list from them.
        var ids = npcIds.Distinct().Take(MaxVoteLookup).ToArray();
        if (ids.Length == 0)
        {
            return new NpcVotesResponse([], []);
        }

        var votes = await voteService.GetVotesAsync(ids, user, ipAddress, cancellationToken);
        return new NpcVotesResponse(
            votes.Where(vote => vote.Value == VoteType.Up).Select(vote => vote.Key).ToArray(),
            votes.Where(vote => vote.Value == VoteType.Down).Select(vote => vote.Key).ToArray());
    }
}
