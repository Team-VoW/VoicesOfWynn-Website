using VoW.Api.Domain.Npcs;

namespace VoW.Api.Repositories;

public interface INpcInteractionRepository
{
    Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken);

    /// <summary>The NPC's display name, or null when no such NPC exists.</summary>
    Task<string?> GetNpcNameAsync(int npcId, CancellationToken cancellationToken);

    /// <summary>
    /// Every recording of an NPC that a visitor may hear, with the quest each belongs to.
    /// </summary>
    Task<IReadOnlyCollection<QuestRecording>> GetRecordingsAsync(int npcId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, QuestName>> GetQuestNamesAsync(
        IReadOnlyCollection<int> questIds,
        CancellationToken cancellationToken);

    /// <summary>Records a vote and returns the recounted totals.</summary>
    Task<NpcVoteCounts> SetVoteAsync(int npcId, string voterId, VoteType vote, CancellationToken cancellationToken);

    /// <summary>Withdraws a vote and returns the recounted totals.</summary>
    Task<NpcVoteCounts> ClearVoteAsync(int npcId, string voterId, CancellationToken cancellationToken);

    /// <summary>The voter's standing votes among the given NPCs, keyed by NPC id.</summary>
    Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        string voterId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NpcComment>> GetCommentsAsync(int npcId, CancellationToken cancellationToken);

    Task<int> InsertCommentAsync(NewNpcComment comment, CancellationToken cancellationToken);

    Task<NpcComment?> GetCommentAsync(int commentId, CancellationToken cancellationToken);

    Task<NpcCommentOwner?> GetCommentOwnerAsync(int commentId, CancellationToken cancellationToken);

    Task<bool> DeleteCommentAsync(int commentId, CancellationToken cancellationToken);
}
