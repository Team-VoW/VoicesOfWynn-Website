using VoW.Api.Domain.Casting;

namespace VoW.Api.Repositories;

/// <summary>Rounds, their characters and the auditions submitted for them.</summary>
public interface ICastingRoundRepository
{
    Task<IReadOnlyList<CastingRound>> GetRoundsAsync(
        IReadOnlyCollection<CastingRoundStatus> statuses,
        CancellationToken cancellationToken);

    Task<CastingRound?> GetRoundAsync(int roundId, CancellationToken cancellationToken);

    /// <summary>The newest round that is not archived for this source, e.g. a Discord quest name.</summary>
    Task<CastingRound?> FindActiveRoundBySourceAsync(
        CastingSource source,
        string sourceRef,
        CancellationToken cancellationToken);

    Task<int> CreateRoundAsync(NewCastingRound round, CancellationToken cancellationToken);

    Task UpdateRoundAsync(int roundId, CastingRoundDetails details, CancellationToken cancellationToken);

    Task SetRoundStatusAsync(int roundId, CastingRoundStatus status, CancellationToken cancellationToken);

    Task SetRoundSourceAsync(int roundId, CastingSource source, string? sourceRef, CancellationToken cancellationToken);

    Task SetImportStateAsync(
        int roundId,
        CastingImportStatus status,
        string? message,
        CancellationToken cancellationToken);

    Task DeleteRoundAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingCharacter>> GetCharactersAsync(int roundId, CancellationToken cancellationToken);

    Task<CastingCharacter?> GetCharacterAsync(int characterId, CancellationToken cancellationToken);

    Task<CastingCharacter?> FindCharacterByNameAsync(int roundId, string name, CancellationToken cancellationToken);

    /// <summary>Appends the character after the existing ones. Returns null when the name is taken.</summary>
    Task<int?> CreateCharacterAsync(int roundId, CastingCharacterDetails details, CancellationToken cancellationToken);

    /// <summary>Returns false when the new name clashes with another character in the round.</summary>
    Task<bool> UpdateCharacterAsync(int characterId, CastingCharacterDetails details, CancellationToken cancellationToken);

    Task DeleteCharacterAsync(int characterId, CancellationToken cancellationToken);

    Task SetWinnerAsync(int characterId, int? auditionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingAudition>> GetAuditionsForRoundAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingAudition>> GetAuditionsAsync(int characterId, CancellationToken cancellationToken);

    Task<CastingAudition?> GetAuditionAsync(int auditionId, CancellationToken cancellationToken);

    Task<CastingAudition?> FindAuditionBySourceAsync(int characterId, string sourceRef, CancellationToken cancellationToken);

    /// <summary>
    /// Numbers the audition after the character's existing ones. Returns null when an audition with the
    /// same source already exists, so concurrent imports of the same clip collapse into one row.
    /// </summary>
    Task<int?> CreateAuditionAsync(NewCastingAudition audition, CancellationToken cancellationToken);

    Task DeleteAuditionAsync(int auditionId, CancellationToken cancellationToken);
}
