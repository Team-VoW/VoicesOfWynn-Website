using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Content;

namespace VoW.Api.Repositories;

public interface IContentRepository
{
    Task<IReadOnlyCollection<ContentOption>> GetQuestsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ContentOption>> GetUsersByRolesAsync(
        IReadOnlyCollection<DiscordRoleId> roles,
        CancellationToken cancellationToken);

    Task<bool> QuestDegeneratedNameExistsAsync(string degeneratedName, CancellationToken cancellationToken);

    Task<CreatedContent> CreateQuestAsync(CreateQuestCommand command, string degeneratedName, CancellationToken cancellationToken);

    Task<CreatedContent> CreateNpcAsync(CreateNpcCommand command, string degeneratedName, CancellationToken cancellationToken);
}
