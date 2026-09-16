using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VoW.Api.Contracts.Mod;
using VoW.Api.Domain.Analytics;
using VoW.Api.Domain.Mod;
using VoW.Api.Repositories;
using VoW.Api.Services.Mod;
using VoW.Api.Services.Security;
using Xunit;

namespace VoW.Api.Tests;

public sealed class ModBootupTests
{
    [Theory]
    // At or below the kill switch the mod must stop; at or below the notification version it warns.
    [InlineData("0", ModUpdateAction.Disable)]
    [InlineData("1.5.0", ModUpdateAction.Disable)]
    [InlineData("1.6.0", ModUpdateAction.Disable)]
    [InlineData("1.6.1", ModUpdateAction.Notify)]
    [InlineData("2.0.2", ModUpdateAction.Notify)]
    [InlineData("2.0.3", ModUpdateAction.None)]
    [InlineData("2.1", ModUpdateAction.None)]
    public async Task VersionDecidesTheAction(string modVersion, ModUpdateAction expected)
    {
        var (service, _, _) = Build();
        var result = await service.BootupAsync(Request(modVersion), "203.0.113.1", default);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Response!.Update.Action);
    }

    /// <summary>
    /// The PHP endpoint returned the ping write's status as the response status. Its duplicate-key
    /// check compared PDO's getCode() against MySQL's 1062 while PDO returns the SQLSTATE '23000',
    /// so every returning player got a 500 - and the mod, which throws on 5xx, silently skipped
    /// the kill switch, the update notice and the broadcasts entirely.
    /// </summary>
    [Fact]
    public async Task AFailingPingStillReturnsTheVersionPayload()
    {
        var (service, analytics, _) = Build();
        analytics.ThrowOnRecord = true;

        var result = await service.BootupAsync(Request("2.0.2"), "203.0.113.1", default);

        Assert.True(result.Succeeded);
        Assert.Equal(ModUpdateAction.Notify, result.Response!.Update.Action);
        Assert.Equal("2.0.3", result.Response.Update.LatestVersion);
    }

    [Fact]
    public async Task RepeatedBootupsFromTheSameClientAllSucceed()
    {
        var (service, analytics, _) = Build();

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await service.BootupAsync(Request("2.0.2"), "203.0.113.1", default)).Succeeded);
        }

        // The throttle suppresses the extra writes; it must not suppress the response.
        Assert.Single(analytics.Pings);
    }

    [Theory]
    [InlineData("two point oh")]
    [InlineData("2.0.x")]
    [InlineData("")]
    public async Task AMalformedVersionIsAValidationFailure(string modVersion)
    {
        var (service, _, _) = Build();
        var result = await service.BootupAsync(Request(modVersion), "203.0.113.1", default);

        Assert.False(result.Succeeded);
        Assert.Contains("ModVersion", result.Errors.Keys);
    }

    [Fact]
    public async Task BroadcastsOutsideTheirWindowAreNotSent()
    {
        var (service, _, config) = Build();
        var now = DateTime.UtcNow;
        config.Broadcasts.Add(new ModBroadcast(1, "live", now.AddDays(-1), now.AddDays(1), now, null));
        config.Broadcasts.Add(new ModBroadcast(2, "over", now.AddDays(-10), now.AddDays(-5), now, null));
        config.Broadcasts.Add(new ModBroadcast(3, "later", now.AddDays(5), now.AddDays(10), now, null));

        var result = await service.BootupAsync(Request("2.0.3"), "203.0.113.1", default);

        Assert.Equal(["live"], result.Response!.Broadcasts);
    }

    [Fact]
    public async Task AnEmptyReleaseTableFailsLoudlyInsteadOfShippingBlankVersions()
    {
        var (service, _, config) = Build();
        config.Release = null;

        var result = await service.BootupAsync(Request("2.0.3"), "203.0.113.1", default);

        Assert.False(result.Succeeded);
    }

    /// <summary>
    /// These cases came across from the mod's own VersionCheckerTest when the comparison moved
    /// server-side. The "multi-digit minor" one guards a real past bug: comparing versions as
    /// floats made 1.14.2 read as 2.42 and therefore newer than 2.0.0.
    /// </summary>
    [Theory]
    [InlineData("2.0.0", "2.0.0", 0)]
    [InlineData("2.0", "2.0.0", 0)]
    [InlineData("2.0.0", "1.14.2", 1)]
    [InlineData("1.14.2", "2.0.0", -1)]
    [InlineData("1.14.2", "1.9.0", 1)]
    [InlineData("1.5.0", "1.4.9", 1)]
    [InlineData("1.0.2", "1.0.1", 1)]
    public void VersionsCompareComponentByComponent(string left, string right, int expected)
    {
        Assert.True(Domain.Mod.ModVersion.TryParse(left, out var a));
        Assert.True(Domain.Mod.ModVersion.TryParse(right, out var b));

        Assert.Equal(expected, Math.Sign(Domain.Mod.ModVersion.Compare(a, b)));
    }

    private static ModBootupRequest Request(string modVersion) => new()
    {
        ClientId = new string('a', 64),
        ModVersion = modVersion,
    };

    private static (ModBootupService Service, MemoryAnalytics Analytics, MemoryModConfig Config) Build()
    {
        var analytics = new MemoryAnalytics();
        var config = new MemoryModConfig();
        var service = new ModBootupService(
            config,
            analytics,
            new ClientAddressHasher(new ConfigurationBuilder().Build()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ModBootupService>.Instance);

        return (service, analytics, config);
    }
}

internal sealed class MemoryModConfig : IModConfigRepository
{
    public ModRelease? Release { get; set; } = new(
        "2.0.3", "2.0.2", "1.6.0",
        "https://example.test/mod.jar", "https://example.test/changelog",
        "https://example.test/sounds/", ["https://mirror.test/sounds/"],
        DateTime.UtcNow, null);

    public List<ModBroadcast> Broadcasts { get; } = [];

    public List<FunFact> FunFacts { get; } = [new(1, "a_fact", "Content", true, DateTime.UtcNow)];

    public Task<ModRelease?> GetReleaseAsync(CancellationToken ct) => Task.FromResult(Release);

    public Task UpdateReleaseAsync(ModRelease release, int? updatedBy, CancellationToken ct)
    {
        Release = release with { UpdatedBy = updatedBy };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModBroadcast>> GetBroadcastsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModBroadcast>>(Broadcasts);

    public Task<IReadOnlyList<string>> GetActiveBroadcastContentsAsync(DateTime at, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(Broadcasts
            .Where(b => b.ActiveFrom <= at && b.ActiveUntil >= at)
            .OrderBy(b => b.ActiveFrom)
            .Select(b => b.Content)
            .ToList());

    public Task<int> CreateBroadcastAsync(ModBroadcast broadcast, CancellationToken ct)
    {
        var id = Broadcasts.Count + 1;
        Broadcasts.Add(broadcast with { Id = id });
        return Task.FromResult(id);
    }

    public Task<bool> UpdateBroadcastAsync(ModBroadcast broadcast, CancellationToken ct)
    {
        var index = Broadcasts.FindIndex(b => b.Id == broadcast.Id);
        if (index < 0) return Task.FromResult(false);
        Broadcasts[index] = broadcast;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteBroadcastAsync(int broadcastId, CancellationToken ct) =>
        Task.FromResult(Broadcasts.RemoveAll(b => b.Id == broadcastId) > 0);

    public Task<IReadOnlyList<FunFact>> GetFunFactsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FunFact>>(FunFacts);

    public Task<string?> GetRandomActiveFunFactAsync(CancellationToken ct) =>
        Task.FromResult(FunFacts.FirstOrDefault(f => f.Active)?.Content);

    public Task<int> CountActiveFunFactsAsync(CancellationToken ct) =>
        Task.FromResult(FunFacts.Count(f => f.Active));

    public Task<int?> CreateFunFactAsync(FunFact funFact, CancellationToken ct)
    {
        if (FunFacts.Any(f => f.Slug == funFact.Slug)) return Task.FromResult<int?>(null);
        var id = FunFacts.Count + 1;
        FunFacts.Add(funFact with { Id = id });
        return Task.FromResult<int?>(id);
    }

    public Task<bool> UpdateFunFactAsync(FunFact funFact, CancellationToken ct)
    {
        var index = FunFacts.FindIndex(f => f.Id == funFact.Id);
        if (index < 0) return Task.FromResult(false);
        FunFacts[index] = funFact;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteFunFactAsync(int funFactId, CancellationToken ct) =>
        Task.FromResult(FunFacts.RemoveAll(f => f.Id == funFactId) > 0);
}

internal sealed class MemoryAnalytics : IAnalyticsRepository
{
    public bool ThrowOnRecord { get; set; }

    public List<(string ClientId, string IpHash, DateTime At)> Pings { get; } = [];

    public Dictionary<DateOnly, int> Daily { get; } = [];

    public Task<IReadOnlyList<DailyUsageRow>> GetDailyUsageAsync(int? days, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyUsageRow>>(
            Daily.OrderBy(entry => entry.Key).Select(entry => new DailyUsageRow(entry.Key, entry.Value)).ToList());

    public Task<int?> GetPreviousPeriodBootupsAsync(int days, CancellationToken ct) => Task.FromResult<int?>(null);

    public Task<bool> RecordBootupAsync(string clientIdHash, string ipHash, CancellationToken ct)
    {
        if (ThrowOnRecord) throw new InvalidOperationException("ping storage unavailable");

        var now = DateTime.UtcNow;
        var throttled = Pings.Any(ping =>
            (ping.ClientId == clientIdHash && now - ping.At < BootupThrottle.ByClientId)
            || (ping.IpHash == ipHash && now - ping.At < BootupThrottle.ByIp));
        if (throttled) return Task.FromResult(false);

        Pings.Add((clientIdHash, ipHash, now));
        return Task.FromResult(true);
    }

    public Task<UsageAggregationResult> AggregateAsync(DateOnly throughDate, int maxDays, CancellationToken ct)
    {
        var days = Pings.Select(ping => DateOnly.FromDateTime(ping.At)).Where(day => day < throughDate)
            .Distinct().OrderBy(day => day).Take(maxDays).ToList();

        var aggregated = 0;
        foreach (var day in days)
        {
            var count = Pings.RemoveAll(ping => DateOnly.FromDateTime(ping.At) == day);
            Daily[day] = Daily.GetValueOrDefault(day) + count;
            aggregated += count;
        }

        return Task.FromResult(new UsageAggregationResult(days.Count, aggregated, days.Count == 0 ? null : days[^1]));
    }
}
