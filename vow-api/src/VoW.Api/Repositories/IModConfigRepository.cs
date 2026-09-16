using VoW.Api.Domain.Mod;

namespace VoW.Api.Repositories;

public interface IModConfigRepository
{
    Task<ModRelease?> GetReleaseAsync(CancellationToken cancellationToken);

    Task UpdateReleaseAsync(ModRelease release, int? updatedBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<ModBroadcast>> GetBroadcastsAsync(CancellationToken cancellationToken);

    /// <summary>Broadcasts whose window contains <paramref name="at"/>, ordered oldest window first.</summary>
    Task<IReadOnlyList<string>> GetActiveBroadcastContentsAsync(DateTime at, CancellationToken cancellationToken);

    Task<int> CreateBroadcastAsync(ModBroadcast broadcast, CancellationToken cancellationToken);

    Task<bool> UpdateBroadcastAsync(ModBroadcast broadcast, CancellationToken cancellationToken);

    Task<bool> DeleteBroadcastAsync(int broadcastId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FunFact>> GetFunFactsAsync(CancellationToken cancellationToken);

    /// <summary>One random active fact, or null when none are active.</summary>
    Task<string?> GetRandomActiveFunFactAsync(CancellationToken cancellationToken);

    Task<int> CountActiveFunFactsAsync(CancellationToken cancellationToken);

    Task<int?> CreateFunFactAsync(FunFact funFact, CancellationToken cancellationToken);

    Task<bool> UpdateFunFactAsync(FunFact funFact, CancellationToken cancellationToken);

    Task<bool> DeleteFunFactAsync(int funFactId, CancellationToken cancellationToken);
}
