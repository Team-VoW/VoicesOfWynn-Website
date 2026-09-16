using VoW.Api.Domain.Contents;

namespace VoW.Api.Repositories;

/// <summary>
/// Reads for the public browsing pages: the quest and NPC indexes, a quest's cast, and an NPC's
/// profile.
/// </summary>
public interface IContentPageRepository
{
    Task<IReadOnlyCollection<QuestListItem>> GetQuestListAsync(CancellationToken cancellationToken);

    /// <summary>The quest addressed by its URL name, or null when no such quest exists.</summary>
    Task<QuestDetail?> GetQuestAsync(string degeneratedName, CancellationToken cancellationToken);

    Task<NpcDetail?> GetNpcAsync(int npcId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the NPC index, narrowed by the criteria's search. Paged rather than whole:
    /// the mod has far more NPCs than quests, and the index is browsed by scrolling.
    /// </summary>
    Task<NpcListPage> GetNpcListAsync(NpcListCriteria criteria, CancellationToken cancellationToken);
}
