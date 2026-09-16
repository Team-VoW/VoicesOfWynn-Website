using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using VoW.Api.Contracts.Mod;
using VoW.Api.Domain.Mod;
using VoW.Api.Services.Mod;
using VoW.Api.Services.Security;
using Xunit;

namespace VoW.Api.Tests;

public sealed class ModConfigTests
{
    [Theory]
    [InlineData("http://example.test/mod.jar")]
    [InlineData("example.test/mod.jar")]
    [InlineData("javascript:alert(1)")]
    public async Task ADownloadLinkMustBeAnAbsoluteHttpsUrl(string downloadUrl)
    {
        var (service, _, _) = Build();

        var result = await service.UpdateReleaseAsync(Release(downloadUrl: downloadUrl), 1, default);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(UpdateModReleaseRequest.DownloadUrl), result.Errors.Keys);
    }

    [Theory]
    [InlineData("2.0.x")]
    [InlineData("v2.0.3")]
    [InlineData("latest")]
    public async Task AVersionMustBeDotSeparatedNumbers(string version)
    {
        var (service, _, _) = Build();

        var result = await service.UpdateReleaseAsync(Release(latestVersion: version), 1, default);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(UpdateModReleaseRequest.LatestVersion), result.Errors.Keys);
    }

    [Fact]
    public async Task AnEditIsVisibleToTheNextBootupDespiteTheCache()
    {
        var (service, config, cache) = Build();
        var bootup = new ModBootupService(
            config,
            new MemoryAnalytics(),
            new ClientAddressHasher(new ConfigurationBuilder().Build()),
            cache,
            NullLogger<ModBootupService>.Instance);

        var request = new ModBootupRequest { ClientId = new string('a', 64), ModVersion = "2.0.3" };
        Assert.Equal(ModUpdateAction.None, (await bootup.BootupAsync(request, "::1", default)).Response!.Update.Action);

        Assert.True((await service.UpdateReleaseAsync(Release(latestVersion: "2.1.0", notify: "2.0.3"), 1, default)).Succeeded);

        Assert.Equal(ModUpdateAction.Notify, (await bootup.BootupAsync(request, "::2", default)).Response!.Update.Action);
    }

    [Fact]
    public async Task ABroadcastWindowMustEndAfterItStarts()
    {
        var (service, _, _) = Build();
        var now = DateTime.UtcNow;

        var (result, _) = await service.CreateBroadcastAsync(
            new SaveBroadcastRequest { Content = "hi", ActiveFrom = now, ActiveUntil = now.AddDays(-1) },
            1,
            default);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SaveBroadcastRequest.ActiveUntil), result.Errors.Keys);
    }

    [Fact]
    public async Task TheLastActiveFunFactCannotBeRemoved()
    {
        var (service, config, _) = Build();
        var onlyFact = config.FunFacts.Single();

        var deleted = await service.DeleteFunFactAsync(onlyFact.Id, default);
        var deactivated = await service.UpdateFunFactAsync(
            onlyFact.Id,
            new SaveFunFactRequest { Slug = onlyFact.Slug, Content = onlyFact.Content, Active = false },
            default);

        Assert.False(deleted.Succeeded);
        Assert.False(deactivated.Succeeded);
        Assert.Single(config.FunFacts);
    }

    [Fact]
    public async Task ADuplicateSlugIsReportedAsAFieldErrorRatherThanAConflictException()
    {
        var (service, config, _) = Build();
        var taken = config.FunFacts.Single().Slug;

        var (result, _) = await service.CreateFunFactAsync(
            new SaveFunFactRequest { Slug = taken, Content = "Another one" },
            default);

        Assert.False(result.Succeeded);
        Assert.Contains(nameof(SaveFunFactRequest.Slug), result.Errors.Keys);
    }

    /// <summary>
    /// Windows are stored as UTC because the mod bootup and the website banner both compare against
    /// UTC. A browser sends its local time as an offset timestamp, so it has to be converted, not
    /// taken at face value.
    /// </summary>
    [Fact]
    public async Task ABroadcastWindowSentWithAnOffsetIsStoredAsUtc()
    {
        var (service, config, _) = Build();
        var from = new DateTimeOffset(2026, 10, 13, 2, 0, 0, TimeSpan.FromHours(2));

        var (result, _) = await service.CreateBroadcastAsync(
            new SaveBroadcastRequest
            {
                Content = "Auditions are open",
                // Kind=Local, as a browser-sent offset timestamp binds to.
                ActiveFrom = from.LocalDateTime,
                ActiveUntil = from.UtcDateTime.AddDays(14),
            },
            1,
            default);

        Assert.True(result.Succeeded);
        var stored = config.Broadcasts.Single();
        Assert.Equal(DateTimeKind.Utc, stored.ActiveFrom.Kind);
        Assert.Equal(from.UtcDateTime, stored.ActiveFrom);
    }

    [Fact]
    public async Task EditingAMissingBroadcastIsANotFound()
    {
        var (service, _, _) = Build();
        var now = DateTime.UtcNow;

        var result = await service.UpdateBroadcastAsync(
            404,
            new SaveBroadcastRequest { Content = "hi", ActiveFrom = now, ActiveUntil = now.AddDays(1) },
            default);

        Assert.False(result.Found);
    }

    private static UpdateModReleaseRequest Release(
        string? downloadUrl = null,
        string? latestVersion = null,
        string? notify = null) => new()
    {
        LatestVersion = latestVersion ?? "2.0.3",
        UpdateNotificationVersion = notify ?? "2.0.2",
        KillSwitchVersion = "1.6.0",
        DownloadUrl = downloadUrl ?? "https://example.test/mod.jar",
        ChangelogUrl = "https://example.test/changelog",
        AudioBaseUrl = "https://example.test/sounds/",
        AudioMirrorUrls = ["https://mirror.test/sounds/"],
    };

    private static (ModConfigService Service, MemoryModConfig Config, IMemoryCache Cache) Build()
    {
        var config = new MemoryModConfig();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new ModConfigService(config, cache), config, cache);
    }
}
