using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;
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
}
