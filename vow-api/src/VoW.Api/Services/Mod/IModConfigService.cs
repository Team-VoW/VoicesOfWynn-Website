using VoW.Api.Contracts.Mod;

namespace VoW.Api.Services.Mod;

public interface IModConfigService
{
    Task<ModReleaseResponse?> GetReleaseAsync(CancellationToken cancellationToken);

    Task<ModConfigMutationResult> UpdateReleaseAsync(
        UpdateModReleaseRequest request,
        int? updatedBy,
        CancellationToken cancellationToken);

    Task<BroadcastListResponse> GetBroadcastsAsync(CancellationToken cancellationToken);

    Task<(ModConfigMutationResult Result, int Id)> CreateBroadcastAsync(
        SaveBroadcastRequest request,
        int? createdBy,
        CancellationToken cancellationToken);

    Task<ModConfigMutationResult> UpdateBroadcastAsync(
        int broadcastId,
        SaveBroadcastRequest request,
        CancellationToken cancellationToken);

    Task<ModConfigMutationResult> DeleteBroadcastAsync(int broadcastId, CancellationToken cancellationToken);

    Task<FunFactListResponse> GetFunFactsAsync(CancellationToken cancellationToken);

    Task<(ModConfigMutationResult Result, int Id)> CreateFunFactAsync(
        SaveFunFactRequest request,
        CancellationToken cancellationToken);

    Task<ModConfigMutationResult> UpdateFunFactAsync(
        int funFactId,
        SaveFunFactRequest request,
        CancellationToken cancellationToken);

    Task<ModConfigMutationResult> DeleteFunFactAsync(int funFactId, CancellationToken cancellationToken);
}
