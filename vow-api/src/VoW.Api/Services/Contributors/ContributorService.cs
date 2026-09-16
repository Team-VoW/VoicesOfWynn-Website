using System.Security.Claims;
using VoW.Api.Contracts.Contributors;
using VoW.Api.Domain.Contributors;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Accounts;
using VoW.Api.Services.Npcs;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Contributors;

public sealed class ContributorService(
    IContributorRepository contributorRepository,
    INpcImageStorage npcImageStorage,
    IQuestScriptStorage questScriptStorage,
    QuestScriptCatalog questScriptCatalog,
    INpcVoteService voteService) : IContributorService
{
    private static readonly int[] AllowedPageSizes = [12, 24, 48, 100];

    public async Task<ContributorListResponse> ListAsync(
        ContributorSearchRequest request,
        CancellationToken cancellationToken)
    {
        var pageSize = AllowedPageSizes.Contains(request.PageSize) ? request.PageSize : 24;
        var page = Math.Max(1, request.Page);

        var result = await contributorRepository.GetContributorsAsync(page, pageSize, cancellationToken);
        return new ContributorListResponse(
            result.Total,
            result.Page,
            result.PageSize,
            result.Results.Select(contributor => new ContributorSummaryResponse(
                contributor.UserId,
                contributor.DisplayName,
                contributor.AvatarUrl,
                contributor.DefaultAvatarUrl,
                contributor.Lore,
                Role(contributor.TopRole),
                Roles(contributor.Roles))).ToArray());
    }

    public async Task<ContributorDetailResponse?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        var profile = await contributorRepository.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var defaultNpcImageUrl = npcImageStorage.GetDefaultImageUrl().ToString();
        var voicing = await contributorRepository.GetVoicedNpcsAsync(userId, cancellationToken);
        var scriptwriting = await contributorRepository.GetWrittenQuestsAsync(userId, cancellationToken);
        var soundEditing = await contributorRepository.GetEditedQuestsAsync(userId, cancellationToken);

        return new ContributorDetailResponse(
            profile.UserId,
            profile.DisplayName,
            profile.AvatarUrl,
            profile.DefaultAvatarUrl,
            profile.Lore,
            // Bios are sanitized when written, but rows predating this API were sanitized by the
            // PHP site under different rules, and the client renders this as markup.
            AccountBioSanitizer.Sanitize(profile.Bio),
            profile.Email,
            profile.Discord,
            profile.Youtube,
            profile.Twitter,
            profile.Instagram,
            profile.Github,
            profile.CastingCallClub,
            Roles(profile.Roles),
            voicing.Select(npc => new VoicedNpcResponse(
                npc.NpcId,
                npc.NpcName,
                npcImageStorage.GetImageUrl(npc.NpcId).ToString(),
                defaultNpcImageUrl,
                npc.Archived,
                npc.Upvotes,
                npc.Downvotes,
                npc.CommentCount,
                npc.RecordingCount,
                npc.Quests.Select(quest => new NpcQuestAppearanceResponse(
                    quest.QuestId,
                    quest.QuestName,
                    quest.QuestDegeneratedName)).ToArray())).ToArray(),
            await ScriptwritingAsync(scriptwriting, cancellationToken),
            soundEditing.Select(quest => new EditedQuestResponse(
                quest.QuestId,
                quest.QuestName,
                quest.QuestDegeneratedName,
                quest.Npcs.Select(npc => new EditedNpcResponse(npc.NpcId, npc.NpcName)).ToArray())).ToArray());
    }

    /// <summary>
    /// Resolves every script URL from one cached listing. The legacy page did one blob existence
    /// check per quest, sequentially, which is the slowest part of rendering a prolific writer's page.
    /// </summary>
    private async Task<IReadOnlyCollection<WrittenQuestResponse>> ScriptwritingAsync(
        IReadOnlyCollection<WrittenQuest> quests,
        CancellationToken cancellationToken)
    {
        if (quests.Count == 0)
        {
            return [];
        }

        var withScripts = await questScriptCatalog.GetScriptNamesAsync(cancellationToken);

        return quests.Select(quest => new WrittenQuestResponse(
            quest.QuestId,
            quest.QuestName,
            quest.QuestDegeneratedName,
            withScripts.Contains(quest.QuestDegeneratedName)
                ? questScriptStorage.GetScriptUrl(quest.QuestDegeneratedName).ToString()
                : null)).ToArray();
    }

    public async Task<ContributorVotesResponse?> GetVotesAsync(
        int userId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var npcIds = await contributorRepository.GetVoicedNpcIdsAsync(userId, cancellationToken);
        if (npcIds is null)
        {
            return null;
        }

        var votes = await voteService.GetVotesAsync(npcIds, user, ipAddress, cancellationToken);

        return new ContributorVotesResponse(
            votes.Where(vote => vote.Value == VoteType.Up).Select(vote => vote.Key).ToArray(),
            votes.Where(vote => vote.Value == VoteType.Down).Select(vote => vote.Key).ToArray());
    }

    private static IReadOnlyCollection<ContributorRoleResponse> Roles(IReadOnlyCollection<ContributorRole> roles) =>
        roles.Select(Role).ToArray();

    private static ContributorRoleResponse Role(ContributorRole role) =>
        new(role.Id, role.Name, role.Color, role.Weight);
}
