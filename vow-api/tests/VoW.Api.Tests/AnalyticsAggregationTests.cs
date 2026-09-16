using VoW.Api.Domain.Analytics;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Analytics;
using Xunit;

namespace VoW.Api.Tests;

public sealed class AnalyticsAggregationTests
{
    [Fact]
    public async Task PingsAreRolledIntoDailyCountsAndTheRawRowsAreRemoved()
    {
        var analytics = new MemoryAnalytics();
        var service = new AnalyticsService(analytics);
        var day = DateTime.UtcNow.AddDays(-10);
        analytics.Pings.Add(("a", "ip1", day));
        analytics.Pings.Add(("b", "ip2", day));
        analytics.Pings.Add(("c", "ip3", day.AddDays(1)));

        var result = await service.AggregateAsync(default);

        Assert.Equal(2, result.DaysProcessed);
        Assert.Equal(3, result.BootupsAggregated);
        Assert.Empty(analytics.Pings);
        Assert.Equal(2, analytics.Daily[DateOnly.FromDateTime(day)]);
    }

    /// <summary>
    /// daily.date had no unique key and the PHP job appended unconditionally, so running it twice
    /// silently doubled a day's number in the chart.
    /// </summary>
    [Fact]
    public async Task RunningTwiceDoesNotDoubleCount()
    {
        var analytics = new MemoryAnalytics();
        var service = new AnalyticsService(analytics);
        var day = DateTime.UtcNow.AddDays(-10);
        analytics.Pings.Add(("a", "ip1", day));

        await service.AggregateAsync(default);
        var second = await service.AggregateAsync(default);

        Assert.Equal(0, second.DaysProcessed);
        Assert.Equal(0, second.BootupsAggregated);
        Assert.Equal(1, analytics.Daily[DateOnly.FromDateTime(day)]);
    }

    [Fact]
    public async Task DaysThatCanStillReceivePingsAreLeftAlone()
    {
        var analytics = new MemoryAnalytics();
        var service = new AnalyticsService(analytics);
        analytics.Pings.Add(("today", "ip", DateTime.UtcNow));
        analytics.Pings.Add(("yesterday", "ip", DateTime.UtcNow.AddDays(-1)));

        var result = await service.AggregateAsync(default);

        // The per-client throttle spans 24h, so the two most recent days are not yet final.
        Assert.Equal(0, result.DaysProcessed);
        Assert.Equal(2, analytics.Pings.Count);
    }

    [Fact]
    public async Task NothingToAggregateIsNotAnError()
    {
        var result = await new AnalyticsService(new MemoryAnalytics()).AggregateAsync(default);

        Assert.Equal(0, result.DaysProcessed);
        Assert.Null(result.ThroughDate);
    }

    [Fact]
    public void TheLagCoversTheLongestThrottleWindowPlusTheDayInProgress()
    {
        Assert.Equal(2, BootupThrottle.AggregationLagDays);
    }
}

public sealed class CapabilityMapperTests
{
    [Theory]
    [InlineData(DiscordRoleId.ProjectDirector)]
    [InlineData(DiscordRoleId.Admin)]
    public void AdminRolesGetSystemAdmin(DiscordRoleId role)
    {
        Assert.Contains(Capability.SystemAdmin, CapabilityMapper.Map([role]));
    }

    /// <summary>
    /// Cast Manager holds every other capability, which is exactly why the Admin page needs one of
    /// its own rather than reusing an existing claim.
    /// </summary>
    [Theory]
    [InlineData(DiscordRoleId.CastManager)]
    [InlineData(DiscordRoleId.Moderator)]
    [InlineData(DiscordRoleId.Developer)]
    [InlineData(DiscordRoleId.SoundEditor)]
    [InlineData(DiscordRoleId.BeginnerActor)]
    public void EveryOtherRoleDoesNot(DiscordRoleId role)
    {
        Assert.DoesNotContain(Capability.SystemAdmin, CapabilityMapper.Map([role]));
    }

    [Fact]
    public void EveryCapabilityHasAClaimValue()
    {
        foreach (var capability in Enum.GetValues<Capability>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CapabilityMapper.ToClaimValue(capability)));
        }

        Assert.Equal(
            Enum.GetValues<Capability>().Length,
            CapabilityMapper.GetAllCapabilities().Distinct().Count());
    }
}
