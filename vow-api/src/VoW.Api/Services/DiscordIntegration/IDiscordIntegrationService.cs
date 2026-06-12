using VoW.Api.Contracts.DiscordIntegration;

namespace VoW.Api.Services.DiscordIntegration;

public interface IDiscordIntegrationService
{
    Task<IReadOnlyCollection<DiscordIntegrationUserResponse>> GetUsersAsync(CancellationToken cancellationToken);

    Task<SyncDiscordUserServiceResult> SyncUserAsync(
        SyncDiscordUserRequest request,
        CancellationToken cancellationToken);
}
