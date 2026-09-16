using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Contributors;

/// <summary>
/// Caches which quests have a script so a cast page costs one storage listing at most, instead of
/// one existence check per quest. Scripts change only on an admin upload, so a short TTL is enough
/// and a stale miss merely hides a freshly uploaded script for under a minute.
/// </summary>
public sealed class QuestScriptCatalog(IQuestScriptStorage questScriptStorage, IMemoryCache cache)
{
    private const string CacheKey = "quest-scripts";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlySet<string>> GetScriptNamesAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var names = await questScriptStorage.ListScriptNamesAsync(cancellationToken);
        cache.Set(CacheKey, names, Ttl);
        return names;
    }
}
