using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Contracts.Mod;
using VoW.Api.Domain.Mod;
using VoW.Api.Repositories;
using VoW.Api.Services.Security;

namespace VoW.Api.Services.Mod;

/// <summary>
/// Everything the mod asks for on launch, in one call: whether it may keep running, where to get
/// a newer build, where the audio lives, a fun fact and any active broadcasts. It also records the
/// bootup ping that feeds the usage analytics.
/// </summary>
public sealed class ModBootupService(
    IModConfigRepository modConfigRepository,
    IAnalyticsRepository analyticsRepository,
    ClientAddressHasher addressHasher,
    IMemoryCache cache,
    ILogger<ModBootupService> logger) : IModBootupService
{
    private const string ReleaseCacheKey = "mod:release";
    private static readonly TimeSpan ReleaseCacheDuration = TimeSpan.FromSeconds(60);

    public async Task<ModBootupServiceResult> BootupAsync(
        ModBootupRequest request,
        string callerIp,
        CancellationToken cancellationToken)
    {
        if (!ModVersion.IsValid(request.ModVersion))
        {
            return ModBootupServiceResult.Failure(
                nameof(request.ModVersion),
                "modVersion must be dot-separated numbers, for example 2.0.3.");
        }

        var release = await GetReleaseAsync(cancellationToken);
        if (release is null)
        {
            logger.LogError("mod_release is empty; the mod cannot be told which version is current.");
            return ModBootupServiceResult.Failure("release", "Mod release configuration is unavailable.");
        }

        await RecordBootupAsync(request.ClientId, callerIp, cancellationToken);

        var funFact = await modConfigRepository.GetRandomActiveFunFactAsync(cancellationToken);
        var broadcasts = await modConfigRepository.GetActiveBroadcastContentsAsync(
            DateTime.UtcNow,
            cancellationToken);

        return ModBootupServiceResult.Success(new ModBootupResponse(
            new ModUpdateResponse(
                ModVersion.DecideAction(request.ModVersion, release),
                release.LatestVersion,
                release.DownloadUrl,
                release.ChangelogUrl),
            new ModAudioResponse(release.AudioBaseUrl, release.AudioMirrorUrls),
            funFact,
            broadcasts));
    }

    /// <remarks>
    /// Analytics must never cost a player their kill switch. The PHP endpoint returned the ping
    /// write's status code as the response status, so a failed write turned the whole call into a
    /// 500 and the mod - which throws on 5xx - silently skipped version checking entirely.
    /// </remarks>
    private async Task RecordBootupAsync(string clientId, string callerIp, CancellationToken cancellationToken)
    {
        try
        {
            await analyticsRepository.RecordBootupAsync(
                clientId,
                addressHasher.Hash(callerIp),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to record a mod bootup ping.");
        }
    }

    private async Task<ModRelease?> GetReleaseAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ReleaseCacheKey, out ModRelease? cached))
        {
            return cached;
        }

        var release = await modConfigRepository.GetReleaseAsync(cancellationToken);
        if (release is not null)
        {
            cache.Set(ReleaseCacheKey, release, ReleaseCacheDuration);
        }

        return release;
    }

    /// <summary>Drops the cached release so an admin edit is visible on the next bootup.</summary>
    public static void InvalidateReleaseCache(IMemoryCache cache) => cache.Remove(ReleaseCacheKey);
}
