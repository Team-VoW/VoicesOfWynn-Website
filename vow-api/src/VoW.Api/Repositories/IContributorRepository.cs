using VoW.Api.Domain.Contributors;

namespace VoW.Api.Repositories;

public interface IContributorRepository
{
    Task<ContributorPage> GetContributorsAsync(int page, int pageSize, CancellationToken cancellationToken);

    Task<ContributorProfile?> GetProfileAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<VoicedNpc>> GetVoicedNpcsAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<WrittenQuest>> GetWrittenQuestsAsync(int userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EditedQuest>> GetEditedQuestsAsync(int userId, CancellationToken cancellationToken);
}
