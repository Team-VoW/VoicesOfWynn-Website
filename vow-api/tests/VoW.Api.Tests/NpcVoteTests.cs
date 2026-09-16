using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using VoW.Api.Domain.Npcs;
using VoW.Api.Services.Npcs;
using Xunit;

namespace VoW.Api.Tests;

public sealed class NpcVoteTests
{
    private const int NpcId = 7;

    [Fact]
    public void AVoterIdIsAlwaysSixtyFourHexCharacters()
    {
        // vote.voter is varchar(64) ascii and the legacy rows are SHA-256 hex, so anything wider
        // would be truncated by the database and silently collide.
        Assert.Matches("^[0-9a-f]{64}$", VoterIdentity.ForUser(42));
        Assert.Matches("^[0-9a-f]{64}$", VoterIdentity.ForAddress("203.0.113.5"));
    }

    [Fact]
    public void ASignedInContributorIsIdentifiedByAccountNotByAddress()
    {
        Assert.Equal(VoterIdentity.ForUser(42), VoterIdentity.ForUser(42));
        Assert.NotEqual(VoterIdentity.ForUser(42), VoterIdentity.ForAddress("203.0.113.5"));
        Assert.NotEqual(VoterIdentity.ForUser(42), VoterIdentity.ForUser(43));
    }

    [Fact]
    public void AnonymousVisitorsBehindOneAddressShareAVote()
    {
        // A known limitation, asserted so it stays a deliberate choice rather than a surprise:
        // only signing in gives a visitor an identity of their own.
        Assert.Equal(VoterIdentity.ForAddress("203.0.113.5"), VoterIdentity.ForAddress("203.0.113.5"));
    }

    [Fact]
    public async Task AContributorsVoteFollowsThemAcrossAddresses()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        await service.SetVoteAsync(NpcId, VoteType.Up, SignedIn(42), "203.0.113.5", default);
        var fromElsewhere = await service.GetVotesAsync([NpcId], SignedIn(42), "198.51.100.9", default);

        Assert.Equal(VoteType.Up, fromElsewhere[NpcId]);
    }

    [Fact]
    public async Task SwitchingAVoteMovesItRatherThanAddingASecond()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        var up = await service.SetVoteAsync(NpcId, VoteType.Up, Anonymous(), "203.0.113.5", default);
        Assert.Equal(1, up.Value!.Upvotes);
        Assert.Equal(0, up.Value.Downvotes);

        var down = await service.SetVoteAsync(NpcId, VoteType.Down, Anonymous(), "203.0.113.5", default);
        Assert.Equal(0, down.Value!.Upvotes);
        Assert.Equal(1, down.Value.Downvotes);
        Assert.Equal(VoteType.Down, down.Value.MyVote);
    }

    [Fact]
    public async Task WithdrawingAVoteClearsItAndTheCount()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        await service.SetVoteAsync(NpcId, VoteType.Up, Anonymous(), "203.0.113.5", default);
        var cleared = await service.ClearVoteAsync(NpcId, Anonymous(), "203.0.113.5", default);

        Assert.Equal(0, cleared.Value!.Upvotes);
        Assert.Null(cleared.Value.MyVote);
        Assert.Empty(await service.GetVotesAsync([NpcId], Anonymous(), "203.0.113.5", default));
    }

    [Fact]
    public async Task TwoPeopleVotingCountSeparately()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        await service.SetVoteAsync(NpcId, VoteType.Up, SignedIn(42), "203.0.113.5", default);
        var second = await service.SetVoteAsync(NpcId, VoteType.Up, SignedIn(43), "203.0.113.5", default);

        Assert.Equal(2, second.Value!.Upvotes);
    }

    [Fact]
    public async Task VotingOnAnNpcThatDoesNotExistIsNotFound()
    {
        var result = await Service(new MemoryNpcs())
            .SetVoteAsync(404, VoteType.Up, Anonymous(), "203.0.113.5", default);

        Assert.False(result.Found);
    }

    private static NpcVoteService Service(MemoryNpcs npcs) => new(npcs, new MemoryWriteLimits());

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(int userId) =>
        new(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
}
