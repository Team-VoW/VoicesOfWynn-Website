using VoW.Api.Domain.Contributors;

namespace VoW.Api.Repositories;

public interface IContributorRepository
{
    Task<ContributorPage> GetContributorsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ContributorProfile?> GetProfileAsync(int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Just the ids of the NPCs a contributor voices, or null when no such user exists. The
    /// per-caller votes endpoint needs nothing else, and the profile queries are far too much
    /// work for one column.
    /// </summary>
    Task<IReadOnlyCollection<int>?> GetVoicedNpcIdsAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VoicedNpc>> GetVoicedNpcsAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WrittenQuest>> GetWrittenQuestsAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EditedQuest>> GetEditedQuestsAsync(int userId, CancellationToken cancellationToken);
}
