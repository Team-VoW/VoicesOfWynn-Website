using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Contracts.Mod;
using VoW.Api.Domain.Mod;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Mod;

/// <summary>
/// Staff-facing editing of everything the mod is told on bootup. This content used to be ini and
/// txt files baked into the PHP image, so bumping a version or scheduling an announcement meant a
/// redeploy.
/// </summary>
public sealed class ModConfigService(
    IModConfigRepository modConfigRepository,
    IMemoryCache cache) : IModConfigService
{
    public async Task<ModReleaseResponse?> GetReleaseAsync(CancellationToken cancellationToken)
    {
        var release = await modConfigRepository.GetReleaseAsync(cancellationToken);
        return release is null ? null : ToResponse(release);
    }

    public async Task<ModConfigMutationResult> UpdateReleaseAsync(
        UpdateModReleaseRequest request,
        int? updatedBy,
        CancellationToken cancellationToken)
    {
        foreach (var (field, value) in new[]
                 {
                     (nameof(request.LatestVersion), request.LatestVersion),
                     (nameof(request.UpdateNotificationVersion), request.UpdateNotificationVersion),
                     (nameof(request.KillSwitchVersion), request.KillSwitchVersion)
                 })
        {
            if (!ModVersion.IsValid(value))
            {
                return ModConfigMutationResult.Invalid(field, "Must be dot-separated numbers, for example 2.0.3.");
            }
        }

        foreach (var (field, value) in new[]
                 {
                     (nameof(request.DownloadUrl), request.DownloadUrl),
                     (nameof(request.ChangelogUrl), request.ChangelogUrl),
                     (nameof(request.AudioBaseUrl), request.AudioBaseUrl)
                 })
        {
            if (!IsHttpsUrl(value))
            {
                return ModConfigMutationResult.Invalid(field, "Must be an absolute https URL.");
            }
        }

        var mirrors = request.AudioMirrorUrls
            .Select(url => url.Trim())
            .Where(url => url.Length > 0)
            .ToList();
        if (mirrors.Any(url => !IsHttpsUrl(url)))
        {
            return ModConfigMutationResult.Invalid(
                nameof(request.AudioMirrorUrls),
                "Every mirror must be an absolute https URL.");
        }

        await modConfigRepository.UpdateReleaseAsync(
            new ModRelease(
                request.LatestVersion.Trim(),
                request.UpdateNotificationVersion.Trim(),
                request.KillSwitchVersion.Trim(),
                request.DownloadUrl.Trim(),
                request.ChangelogUrl.Trim(),
                request.AudioBaseUrl.Trim(),
                mirrors,
                DateTime.UtcNow,
                updatedBy),
            updatedBy,
            cancellationToken);

        ModBootupService.InvalidateReleaseCache(cache);
        return ModConfigMutationResult.Success();
    }

    public async Task<BroadcastListResponse> GetBroadcastsAsync(CancellationToken cancellationToken)
    {
        var broadcasts = await modConfigRepository.GetBroadcastsAsync(cancellationToken);
        return new BroadcastListResponse(broadcasts.Select(ToResponse).ToList());
    }

    public async Task<(ModConfigMutationResult Result, int Id)> CreateBroadcastAsync(
        SaveBroadcastRequest request,
        int? createdBy,
        CancellationToken cancellationToken)
    {
        var validation = ValidateWindow(request);
        if (!validation.Succeeded)
        {
            return (validation, 0);
        }

        var id = await modConfigRepository.CreateBroadcastAsync(
            new ModBroadcast(0, request.Content.Trim(), ToUtc(request.ActiveFrom), ToUtc(request.ActiveUntil), DateTime.UtcNow, createdBy),
            cancellationToken);

        return (ModConfigMutationResult.Success(), id);
    }

    public async Task<ModConfigMutationResult> UpdateBroadcastAsync(
        int broadcastId,
        SaveBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateWindow(request);
        if (!validation.Succeeded)
        {
            return validation;
        }

        var found = await modConfigRepository.UpdateBroadcastAsync(
            new ModBroadcast(broadcastId, request.Content.Trim(), ToUtc(request.ActiveFrom), ToUtc(request.ActiveUntil), default, null),
            cancellationToken);

        return found ? ModConfigMutationResult.Success() : ModConfigMutationResult.NotFound();
    }

    public async Task<ModConfigMutationResult> DeleteBroadcastAsync(int broadcastId, CancellationToken cancellationToken) =>
        await modConfigRepository.DeleteBroadcastAsync(broadcastId, cancellationToken)
            ? ModConfigMutationResult.Success()
            : ModConfigMutationResult.NotFound();

    public async Task<FunFactListResponse> GetFunFactsAsync(CancellationToken cancellationToken)
    {
        var funFacts = await modConfigRepository.GetFunFactsAsync(cancellationToken);
        return new FunFactListResponse(funFacts.Select(ToResponse).ToList());
    }

    public async Task<(ModConfigMutationResult Result, int Id)> CreateFunFactAsync(
        SaveFunFactRequest request,
        CancellationToken cancellationToken)
    {
        var id = await modConfigRepository.CreateFunFactAsync(
            new FunFact(0, request.Slug.Trim(), request.Content.Trim(), request.Active, DateTime.UtcNow),
            cancellationToken);

        return id is null
            ? (ModConfigMutationResult.Invalid(nameof(request.Slug), "A fun fact with this slug already exists."), 0)
            : (ModConfigMutationResult.Success(), id.Value);
    }

    public async Task<ModConfigMutationResult> UpdateFunFactAsync(
        int funFactId,
        SaveFunFactRequest request,
        CancellationToken cancellationToken)
    {
        var deactivating = !request.Active;
        if (deactivating && !await WouldLeaveAnActiveFactAsync(funFactId, cancellationToken))
        {
            return ModConfigMutationResult.Invalid(
                nameof(request.Active),
                "At least one fun fact must stay active - the mod shows one on every launch.");
        }

        var found = await modConfigRepository.UpdateFunFactAsync(
            new FunFact(funFactId, request.Slug.Trim(), request.Content.Trim(), request.Active, default),
            cancellationToken);

        return found ? ModConfigMutationResult.Success() : ModConfigMutationResult.NotFound();
    }

    public async Task<ModConfigMutationResult> DeleteFunFactAsync(int funFactId, CancellationToken cancellationToken)
    {
        if (!await WouldLeaveAnActiveFactAsync(funFactId, cancellationToken))
        {
            return ModConfigMutationResult.Invalid(
                "funFactId",
                "At least one fun fact must remain - the mod shows one on every launch.");
        }

        return await modConfigRepository.DeleteFunFactAsync(funFactId, cancellationToken)
            ? ModConfigMutationResult.Success()
            : ModConfigMutationResult.NotFound();
    }

    private async Task<bool> WouldLeaveAnActiveFactAsync(int funFactId, CancellationToken cancellationToken)
    {
        var funFacts = await modConfigRepository.GetFunFactsAsync(cancellationToken);
        return funFacts.Any(fact => fact.Active && fact.Id != funFactId);
    }

    /// <summary>
    /// Broadcast windows are stored as UTC. A caller that sent an offset gets converted; one that
    /// sent a bare timestamp is taken at its word, which is what the API documents.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static ModConfigMutationResult ValidateWindow(SaveBroadcastRequest request) =>
        request.ActiveFrom >= request.ActiveUntil
            ? ModConfigMutationResult.Invalid(nameof(request.ActiveUntil), "The end of the window must be after its start.")
            : ModConfigMutationResult.Success();

    private static bool IsHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static ModReleaseResponse ToResponse(ModRelease release) =>
        new(release.LatestVersion,
            release.UpdateNotificationVersion,
            release.KillSwitchVersion,
            release.DownloadUrl,
            release.ChangelogUrl,
            release.AudioBaseUrl,
            release.AudioMirrorUrls,
            release.UpdatedAt,
            release.UpdatedBy);

    private static BroadcastResponse ToResponse(ModBroadcast broadcast) =>
        new(broadcast.Id, broadcast.Content, broadcast.ActiveFrom, broadcast.ActiveUntil, broadcast.CreatedAt, broadcast.CreatedBy);

    private static FunFactResponse ToResponse(FunFact funFact) =>
        new(funFact.Id, funFact.Slug, funFact.Content, funFact.Active, funFact.CreatedAt);
}
