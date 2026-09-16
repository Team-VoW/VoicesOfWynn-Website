using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Contracts.Content;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Content;

/// <summary>
/// Finds NPCs by name and tells staff where each was last heard, so an unvoiced-line report can be
/// traced back to a place in the world. Spans both databases: the NPC roster lives in the website
/// schema, the sightings in the api schema.
/// </summary>
public sealed class NpcLookupService(
    IContentRepository contentRepository,
    IReportRepository reportRepository,
    INpcImageStorage npcImageStorage,
    IMemoryCache cache) : INpcLookupService
{
    private const string ImageIdsCacheKey = "npc:image-ids";
    private static readonly TimeSpan ImageIdsCacheDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How many name matches to pull before the missing-picture filter runs. The legacy endpoint
    /// applied LIMIT first and filtered afterwards, so asking for 100 could return anything from 0
    /// to 100 with no way to reach the rest.
    /// </summary>
    private const int FilterCandidateLimit = 2000;

    public async Task<NpcLookupResponse> SearchAsync(
        NpcLookupRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Q.Trim();
        if (query.Length == 0)
        {
            return new NpcLookupResponse([]);
        }

        var candidateLimit = request.MissingPicture ? FilterCandidateLimit : request.Limit;
        var matches = await contentRepository.SearchNpcsByNameAsync(query, candidateLimit, cancellationToken);

        if (request.MissingPicture)
        {
            var withImages = await GetNpcIdsWithImagesAsync(cancellationToken);
            matches = matches.Where(match => !withImages.Contains(match.NpcId)).ToList();
        }

        matches = matches.Take(request.Limit).ToList();
        if (matches.Count == 0)
        {
            return new NpcLookupResponse([]);
        }

        // Looked up by the degenerated name. The mod reports an NPC under that form, so it is the
        // only form report.npc_name holds - matching the display name instead resolves nothing for
        // any NPC whose name contains a space.
        var positions = await reportRepository.GetLastPositionsByNpcNameAsync(
            matches.Select(match => match.DegeneratedName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken);

        return new NpcLookupResponse(matches.Select(match => new NpcLookupResult(
            match.NpcId,
            match.Name,
            positions.TryGetValue(match.DegeneratedName, out var position)
                ? new NpcLastSeenResponse(position.X, position.Y, position.Z)
                : null)).ToList());
    }

    private async Task<IReadOnlySet<int>> GetNpcIdsWithImagesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(ImageIdsCacheKey, out IReadOnlySet<int>? cached) && cached is not null)
        {
            return cached;
        }

        var ids = await npcImageStorage.ListNpcIdsWithImagesAsync(cancellationToken);
        cache.Set(ImageIdsCacheKey, ids, ImageIdsCacheDuration);
        return ids;
    }
}
