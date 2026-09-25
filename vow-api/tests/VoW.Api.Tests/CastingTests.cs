using System.Text.Json;
using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Casting;
using VoW.Api.Services.Casting;
using Xunit;

namespace VoW.Api.Tests;

public sealed class CastingTests
{
    private const int Alice = 1;
    private const int Bob = 2;
    private const int Carol = 3;
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryCastingRounds rounds = new();
    private readonly MemoryCastingVotes votes;
    private readonly MemoryCastingAudio audio = new();
    private readonly FixedTime time = new(Now);

    public CastingTests()
    {
        votes = new MemoryCastingVotes(rounds);
        votes.Users.Add((new CastingVoter(Alice, "Alice"), DiscordRoleId.CastManager));
        votes.Users.Add((new CastingVoter(Bob, "Bob"), DiscordRoleId.VoiceManager));
        votes.Users.Add((new CastingVoter(Carol, "Carol"), DiscordRoleId.Admin));
    }

    [Theory]
    [InlineData(DiscordRoleId.VoiceManager, true, false)]
    [InlineData(DiscordRoleId.CastManager, true, true)]
    [InlineData(DiscordRoleId.Admin, true, true)]
    [InlineData(DiscordRoleId.ProjectDirector, true, true)]
    [InlineData(DiscordRoleId.Writer, false, false)]
    [InlineData(DiscordRoleId.Moderator, false, false)]
    public void OnlyTheCastingRolesVoteAndOnlyManagersRunRounds(DiscordRoleId role, bool votesExpected, bool managesExpected)
    {
        var capabilities = CapabilityMapper.Map([role]);

        Assert.Equal(votesExpected, capabilities.Contains(Capability.CastingVote));
        Assert.Equal(managesExpected, capabilities.Contains(Capability.CastingManage));
    }

    [Fact]
    public async Task VotersNeverSeeDraftOrClosedRounds()
    {
        rounds.AddRound(CastingRoundStatus.Draft);
        var open = rounds.AddRound(CastingRoundStatus.Open);
        var closed = rounds.AddRound(CastingRoundStatus.Closed);
        var service = Voting();

        var list = await service.GetOpenRoundsAsync(Alice, default);

        Assert.Equal([open], list.Rounds.Select(r => r.Id));
        Assert.Null(await service.GetRoundAsync(closed, Alice, default));
    }

    [Fact]
    public async Task VotingIsRejectedOncePastTheClosingTime()
    {
        var (_, _, audition) = Seed(closesAt: Now.UtcDateTime.AddMinutes(-1));

        var result = await Voting().VoteAsync(audition, Alice, "great", default);

        Assert.False(result.Succeeded);
        Assert.Contains("round", result.Errors.Keys);
        Assert.Empty(votes.Votes);
    }

    [Fact]
    public async Task VotesAreFrozenOnceTheCharacterIsDoneUntilReopened()
    {
        var (_, character, audition) = Seed();
        var service = Voting();

        await service.SetDoneAsync(character, Alice, true, default);
        var whileDone = await service.VoteAsync(audition, Alice, null, default);
        await service.SetDoneAsync(character, Alice, false, default);
        var afterReopen = await service.VoteAsync(audition, Alice, null, default);

        Assert.Contains("character", whileDone.Errors.Keys);
        Assert.True(afterReopen.Succeeded);
    }

    [Fact]
    public async Task OtherCommentsStayHiddenUntilTheVoterIsDone()
    {
        var (_, character, audition) = Seed();
        var service = Voting();
        await service.VoteAsync(audition, Bob, "Nails the direction", default);

        var before = await service.GetAuditionsAsync(character, Alice, default);
        await service.SetDoneAsync(character, Alice, true, default);
        var after = await service.GetAuditionsAsync(character, Alice, default);

        Assert.False(before!.CommentsRevealed);
        Assert.Empty(before.Auditions.Single().AnonymousComments);
        Assert.True(after!.CommentsRevealed);
        Assert.Equal(["Nails the direction"], after.Auditions.Single().AnonymousComments);
    }

    [Fact]
    public async Task AVoterSeesTheirOwnCommentButNeverAnotherVotersIdentityOrCount()
    {
        var (_, character, audition) = Seed();
        var service = Voting();
        await service.VoteAsync(audition, Bob, "Bob's secret take", default);
        await service.VoteAsync(audition, Alice, "mine", default);
        await service.SetDoneAsync(character, Alice, true, default);

        var list = await service.GetAuditionsAsync(character, Alice, default);
        var json = JsonSerializer.Serialize(list);

        var row = list!.Auditions.Single();
        Assert.True(row.MyVote);
        Assert.Equal("mine", row.MyComment);
        Assert.Equal(["Bob's secret take"], row.AnonymousComments);
        Assert.DoesNotContain("Bob\"", json);
        Assert.DoesNotContain("UserId", json);
        Assert.DoesNotContain("VoteCount", json);
    }

    [Fact]
    public async Task BlankCommentsAreStoredAsNoComment()
    {
        var (_, _, audition) = Seed();

        await Voting().VoteAsync(audition, Alice, "   ", default);

        Assert.Null(votes.Votes.Single().Comment);
    }

    [Fact]
    public async Task RemovingAVoteKeepsTheComment()
    {
        var (_, character, audition) = Seed();
        var service = Voting();
        await service.VoteAsync(audition, Alice, "Warm and tired", default);

        await service.RemoveVoteAsync(audition, Alice, default);
        var row = (await service.GetAuditionsAsync(character, Alice, default))!.Auditions.Single();

        Assert.False(row.MyVote);
        Assert.Equal("Warm and tired", row.MyComment);
    }

    [Fact]
    public async Task ACommentCanBeLeftWithoutVotingAndDoesNotCountAsAPick()
    {
        var (round, character, audition) = Seed();
        var service = Voting();

        await service.SetCommentAsync(audition, Alice, "Good energy, wrong age", default);
        await service.SetCommentAsync(audition, Bob, "Too quiet", default);
        await service.SetDoneAsync(character, Bob, true, default);
        var detail = await service.GetRoundAsync(round, Alice, default);
        var bobsView = (await service.GetAuditionsAsync(character, Bob, default))!.Auditions.Single();
        var review = await new CastingReviewService(rounds, votes, audio).GetReviewAsync(round, default);

        Assert.Equal(0, detail!.Characters.Single().MyPickCount);
        Assert.False(bobsView.MyVote);
        Assert.Equal(["Good energy, wrong age"], bobsView.AnonymousComments);
        var reviewed = review!.Characters.Single();
        Assert.Equal(0, reviewed.TotalVotes);
        Assert.Equal(0, reviewed.Auditions.Single().VoteCount);
        Assert.All(reviewed.Auditions.Single().Votes, v => Assert.False(v.Picked));
        Assert.Equal(["Bob"], reviewed.AbstainedVoters);
    }

    [Fact]
    public async Task DeletingACommentKeepsThePickAndClearingAllKeepsComments()
    {
        var (_, character, first) = Seed();
        var second = rounds.AddAudition(character, "Second");
        var service = Voting();
        await service.VoteAsync(first, Alice, "keep me", default);
        await service.VoteAsync(second, Alice, "remove me", default);

        await service.DeleteCommentAsync(second, Alice, default);
        await service.ClearVotesAsync(character, Alice, default);
        var rows = (await service.GetAuditionsAsync(character, Alice, default))!.Auditions;

        Assert.All(rows, r => Assert.False(r.MyVote));
        Assert.Equal("keep me", rows.Single(r => r.Id == first).MyComment);
        Assert.Null(rows.Single(r => r.Id == second).MyComment);
        Assert.Single(votes.Votes);
    }

    [Fact]
    public async Task ABlankCommentDeletesIt()
    {
        var (_, _, audition) = Seed();
        var service = Voting();
        await service.SetCommentAsync(audition, Alice, "first thought", default);

        await service.SetCommentAsync(audition, Alice, "  ", default);

        Assert.Empty(votes.Votes);
    }

    [Theory]
    [InlineData(CastingRoundStatus.Draft, CastingRoundStatus.Open, true)]
    [InlineData(CastingRoundStatus.Open, CastingRoundStatus.Closed, true)]
    [InlineData(CastingRoundStatus.Closed, CastingRoundStatus.Archived, true)]
    [InlineData(CastingRoundStatus.Closed, CastingRoundStatus.Open, true)]
    [InlineData(CastingRoundStatus.Draft, CastingRoundStatus.Archived, false)]
    [InlineData(CastingRoundStatus.Archived, CastingRoundStatus.Open, false)]
    public void RoundsMoveThroughTheirLifecycleInOrder(CastingRoundStatus from, CastingRoundStatus to, bool allowed) =>
        Assert.Equal(allowed, CastingAdminService.CanTransition(from, to));

    [Fact]
    public async Task ARoundWithoutAuditionsCannotBeOpened()
    {
        var round = rounds.AddRound(CastingRoundStatus.Draft);
        rounds.AddCharacter(round);

        var result = await Admin().SetStatusAsync(round, CastingRoundStatus.Open, default);

        Assert.Contains("status", result.Errors.Keys);
    }

    [Fact]
    public async Task OnlyDraftRoundsCanBeDeletedAndTheirAudioGoesWithThem()
    {
        var (round, character, _) = Seed(status: CastingRoundStatus.Draft);
        var service = Admin();
        await service.UploadAuditionAsync(character, "Someone", new MemoryStream([1, 2, 3]), default);
        Assert.Single(audio.Blobs);

        var deleted = await service.DeleteRoundAsync(round, default);
        var closedRound = rounds.AddRound(CastingRoundStatus.Closed);
        var refused = await service.DeleteRoundAsync(closedRound, default);

        Assert.True(deleted.Succeeded);
        Assert.Empty(audio.Blobs);
        Assert.Contains("status", refused.Errors.Keys);
    }

    [Fact]
    public async Task CccImportsOnlyAcceptCastingCallClubLinks()
    {
        var round = rounds.AddRound(CastingRoundStatus.Draft);
        var queue = new RecordingImportQueue();
        var service = Admin(queue);

        var rejected = await service.StartCccImportAsync(round, "https://evil.example/project", default);
        var accepted = await service.StartCccImportAsync(round, "https://www.castingcall.club/projects/voices-of-wynn", default);

        Assert.Contains("url", rejected.Errors.Keys);
        Assert.True(accepted.Succeeded);
        Assert.Single(queue.Jobs);
        Assert.Equal(CastingSource.Ccc, rounds.Rounds.Single().Source);
    }

    [Fact]
    public async Task TheReviewRanksAuditionsAndNamesVotersAndStragglers()
    {
        var (round, character, first) = Seed();
        var second = rounds.AddAudition(character, "Second");
        var service = Voting();
        await service.VoteAsync(second, Alice, "best", default);
        await service.VoteAsync(second, Bob, null, default);
        await service.VoteAsync(first, Bob, null, default);
        await service.SetDoneAsync(character, Alice, true, default);
        await service.SetDoneAsync(character, Carol, true, default);

        var review = await new CastingReviewService(rounds, votes, audio).GetReviewAsync(round, default);

        var reviewed = review!.Characters.Single();
        Assert.Equal(3, review.EligibleVoterCount);
        Assert.Equal([second, first], reviewed.Auditions.Select(a => a.Id));
        Assert.Equal(["Alice", "Bob"], reviewed.Auditions[0].Votes.Select(v => v.VoterName));
        Assert.Equal(["Alice", "Carol"], reviewed.DoneVoters);
        Assert.Equal(["Carol"], reviewed.AbstainedVoters);
        Assert.Equal(["Bob"], reviewed.PendingVoters);
    }

    [Fact]
    public async Task TheBotCanResendTheSameThreadWithoutDuplicatingIt()
    {
        var service = new CastingBotService(rounds, new NoUsers(), Ingest(), new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var first = await service.EnsureRoundAsync(new BotEnsureCastingRoundRequest { QuestName = "Ragni", Characters = ["Guard"] }, default);
        var again = await service.EnsureRoundAsync(new BotEnsureCastingRoundRequest { QuestName = "Ragni", Characters = ["Guard", "Mayor"] }, default);
        var request = new BotUploadAuditionRequest { CharacterName = "Guard", AuditioneeName = "kmaxi", DiscordThreadId = "123" };
        var upload = await service.AddAuditionAsync(first.RoundId, request, new MemoryStream([1]), default);
        var resend = await service.AddAuditionAsync(first.RoundId, request, new MemoryStream([1]), default);

        Assert.True(first.Created);
        Assert.False(again.Created);
        Assert.Equal(first.RoundId, again.RoundId);
        Assert.Equal(2, rounds.Characters.Count);
        Assert.True(upload.Value!.Created);
        Assert.False(resend.Value!.Created);
        Assert.Single(rounds.Auditions);
        Assert.Single(audio.Blobs);
        Assert.EndsWith($"/admin/casting/{first.RoundId}", first.AdminUrl);
    }

    [Fact]
    public void CccPagesAreParsedAndTheProjectIdIsFound()
    {
        const string page = """
            {"submissions":[
              {"roleName":" Theorick ","username":"VelvetVoice","audioUrl":"https://cdn.ccc/a.mp3"},
              {"roleName":"Theorick","username":null,"audioUrl":"https://cdn.ccc/b.mp3"},
              {"roleName":"Broken","username":"x"}
            ]}
            """;

        var (submissions, raw) = CccClient.ParseSubmissionsPage(page);

        Assert.Equal(2, submissions.Count);
        Assert.Equal("Theorick", submissions[0].RoleName);
        Assert.Equal("unknown", submissions[1].Username);
        Assert.StartsWith("[", raw);
        Assert.Equal("1234", CccClient.ExtractProjectId("""<a href="/manage/submissions?tab=a&project_id=1234">Manage</a>"""));
        Assert.Null(CccClient.ExtractProjectId("<html></html>"));
    }

    [Theory]
    [InlineData("https://www.castingcall.club/projects/x", true)]
    [InlineData("https://castingcall.club/projects/x", true)]
    [InlineData("http://www.castingcall.club/projects/x", false)]
    [InlineData("https://www.castingcall.club.evil.example/x", false)]
    [InlineData("not a url", false)]
    public void OnlyHttpsCastingCallClubLinksCountAsCcc(string url, bool expected) =>
        Assert.Equal(expected, CccClient.IsCccUrl(url));

    private (int Round, int Character, int Audition) Seed(
        CastingRoundStatus status = CastingRoundStatus.Open,
        DateTime? closesAt = null)
    {
        var round = rounds.AddRound(status, closesAt);
        var character = rounds.AddCharacter(round);
        return (round, character, rounds.AddAudition(character, "First"));
    }

    private CastingVotingService Voting() => new(rounds, votes, audio, time);

    private CastingAuditionIngestService Ingest() => new(rounds, new PassThroughTranscoder(), audio);

    private CastingAdminService Admin(RecordingImportQueue? queue = null) =>
        new(rounds, audio, Ingest(), queue ?? new RecordingImportQueue(), time);

    private sealed class NoUsers : VoW.Api.Repositories.IUserRepository
    {
        public Task<VoW.Api.Domain.Users.UserProfile?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken) =>
            Task.FromResult<VoW.Api.Domain.Users.UserProfile?>(null);

        public Task<VoW.Api.Domain.Users.UserProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult<VoW.Api.Domain.Users.UserProfile?>(null);

        public Task<VoW.Api.Domain.Auth.PasswordLoginUser?> GetForPasswordLoginAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult<VoW.Api.Domain.Auth.PasswordLoginUser?>(null);
    }
}
