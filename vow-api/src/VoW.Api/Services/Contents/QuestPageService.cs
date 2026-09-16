using System.Security.Claims;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Contributors;
using VoW.Api.Services.Npcs;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Contents;

public sealed class QuestPageService(
    IContentPageRepository repository,
    INpcImageStorage npcImageStorage,
    IQuestScriptStorage questScriptStorage,
    QuestScriptCatalog questScriptCatalog,
    INpcVoteService voteService) : IQuestPageService
{
    public async Task<QuestListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var quests = await repository.GetQuestListAsync(cancellationToken);
        return new QuestListResponse(quests.Select(quest => new QuestListItemResponse(
            quest.QuestId,
            quest.QuestName,
            quest.QuestDegeneratedName,
            quest.NpcCount,
            quest.RecordingCount)).ToArray());
    }

    public async Task<QuestDetailResponse?> GetAsync(string degeneratedName, CancellationToken cancellationToken)
    {
        var quest = await repository.GetQuestAsync(degeneratedName, cancellationToken);
        if (quest is null)
        {
            return null;
        }

        var defaultNpcImageUrl = npcImageStorage.GetDefaultImageUrl().ToString();
        // One cached listing beats a per-quest existence check: the same catalog already serves
        // the cast pages, so a visitor moving between them costs storage nothing.
        var withScripts = await questScriptCatalog.GetScriptNamesAsync(cancellationToken);

        return new QuestDetailResponse(
            quest.QuestId,
            quest.QuestName,
            quest.QuestDegeneratedName,
            withScripts.Contains(quest.QuestDegeneratedName)
                ? questScriptStorage.GetScriptUrl(quest.QuestDegeneratedName).ToString()
                : null,
            ContentCreditMapper.Credit(quest.Writer),
            quest.Npcs.Select(npc => new QuestNpcResponse(
                npc.NpcId,
                npc.NpcName,
                npcImageStorage.GetImageUrl(npc.NpcId).ToString(),
                defaultNpcImageUrl,
                npc.Archived,
                npc.Upvotes,
                npc.Downvotes,
                npc.CommentCount,
                npc.RecordingCount,
                ContentCreditMapper.Credit(npc.VoiceActor),
                ContentCreditMapper.Credit(npc.SoundEditor))).ToArray());
    }

    public async Task<NpcVotesResponse?> GetVotesAsync(
        string degeneratedName,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var quest = await repository.GetQuestAsync(degeneratedName, cancellationToken);
        if (quest is null)
        {
            return null;
        }

        var votes = await voteService.GetVotesAsync(
            quest.Npcs.Select(npc => npc.NpcId).ToArray(),
            user,
            ipAddress,
            cancellationToken);

        return new NpcVotesResponse(
            votes.Where(vote => vote.Value == VoteType.Up).Select(vote => vote.Key).ToArray(),
            votes.Where(vote => vote.Value == VoteType.Down).Select(vote => vote.Key).ToArray());
    }
}
