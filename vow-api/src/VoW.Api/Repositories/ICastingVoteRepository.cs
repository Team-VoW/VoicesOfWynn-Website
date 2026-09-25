using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Casting;

namespace VoW.Api.Repositories;

/// <summary>Votes, comments and "character done" marks. Voter identities never leave the review service.</summary>
public interface ICastingVoteRepository
{
    Task<IReadOnlyList<CastingVote>> GetVotesForRoundAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingVote>> GetVotesForCharacterAsync(int characterId, CancellationToken cancellationToken);

    /// <summary>Picks the audition and stores the comment alongside it (null clears it).</summary>
    Task UpsertVoteAsync(int auditionId, int userId, string? comment, CancellationToken cancellationToken);

    /// <summary>Sets the comment without touching whether the audition is picked.</summary>
    Task UpsertCommentAsync(int auditionId, int userId, string comment, CancellationToken cancellationToken);

    /// <summary>Withdraws the pick but keeps any comment.</summary>
    Task UnpickAsync(int auditionId, int userId, CancellationToken cancellationToken);

    /// <summary>Withdraws every pick on the character, keeping comments.</summary>
    Task UnpickAllForCharacterAsync(int characterId, int userId, CancellationToken cancellationToken);

    /// <summary>Removes the comment; the pick, if any, stays.</summary>
    Task DeleteCommentAsync(int auditionId, int userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingDone>> GetDoneForRoundAsync(int roundId, CancellationToken cancellationToken);

    Task SetDoneAsync(int characterId, int userId, CancellationToken cancellationToken);

    Task ClearDoneAsync(int characterId, int userId, CancellationToken cancellationToken);

    /// <summary>Everyone currently holding one of the voter roles.</summary>
    Task<IReadOnlyList<CastingVoter>> GetUsersWithRolesAsync(
        IReadOnlyCollection<DiscordRoleId> roles,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingVoter>> GetUsersAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken);
}
