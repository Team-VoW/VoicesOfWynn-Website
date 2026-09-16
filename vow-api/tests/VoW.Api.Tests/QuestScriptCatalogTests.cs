using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Services.Contributors;
using VoW.Api.Services.Storage;
using Xunit;

namespace VoW.Api.Tests;

public sealed class QuestScriptCatalogTests
{
    [Fact]
    public async Task ResolvingManyQuestsCostsOneStorageCallAndNoExistenceChecks()
    {
        // The legacy page issued one blob existence check per quest, serially. This is the guard
        // against that creeping back in.
        var storage = new CountingScriptStorage(["a-quest", "another-quest"]);
        var catalog = new QuestScriptCatalog(storage, new MemoryCache(new MemoryCacheOptions()));

        var names = await catalog.GetScriptNamesAsync(default);

        Assert.Equal(1, storage.ListCalls);
        Assert.Equal(0, storage.ExistsCalls);
        Assert.Contains("a-quest", names);
        Assert.DoesNotContain("missing-quest", names);
    }

    [Fact]
    public async Task RepeatedLookupsReuseTheCachedListing()
    {
        var storage = new CountingScriptStorage(["a-quest"]);
        var catalog = new QuestScriptCatalog(storage, new MemoryCache(new MemoryCacheOptions()));

        await catalog.GetScriptNamesAsync(default);
        await catalog.GetScriptNamesAsync(default);

        Assert.Equal(1, storage.ListCalls);
    }

    private sealed class CountingScriptStorage(string[] names) : IQuestScriptStorage
    {
        public int ListCalls { get; private set; }

        public int ExistsCalls { get; private set; }

        public Task<IReadOnlySet<string>> ListScriptNamesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlySet<string>>(names.ToHashSet(StringComparer.Ordinal));
        }

        public Task<bool> ScriptExistsAsync(string degeneratedName, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            return Task.FromResult(names.Contains(degeneratedName));
        }

        public Uri GetScriptUrl(string degeneratedName) =>
            new($"https://example.test/scripts/{degeneratedName}.txt");

        public Task UploadScriptAsync(string degeneratedName, Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
