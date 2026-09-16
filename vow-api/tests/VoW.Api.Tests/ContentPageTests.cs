using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using VoW.Api.Contracts.Contents;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Domain.Contents;
using VoW.Api.Domain.Contributors;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Contents;
using VoW.Api.Services.Contributors;
using VoW.Api.Services.Npcs;
using VoW.Api.Services.Storage;
using Xunit;

namespace VoW.Api.Tests;

public sealed class ContentPageTests
{
    private const string QuestName = "flightindistress";

    /// <summary>The id <see cref="MemoryNpcs"/> knows, so the existence check in the service passes.</summary>
    internal const int CastNpcId = 7;

    [Fact]
    public async Task AQuestWithAnUploadedScriptLinksToItAndOneWithoutDoesNot()
    {
        var withScript = await QuestService(scripts: [QuestName]).GetAsync(QuestName, default);
        var withoutScript = await QuestService(scripts: []).GetAsync(QuestName, default);

        Assert.Equal("https://storage.test/scripts/flightindistress.txt", withScript!.ScriptUrl);
        Assert.Null(withoutScript!.ScriptUrl);
    }

    [Fact]
    public async Task AnUnknownQuestIsNullSoTheControllerCanAnswerNotFound()
    {
        Assert.Null(await QuestService().GetAsync("no-such-quest", default));
        Assert.Null(await QuestService().GetVotesAsync("no-such-quest", Anonymous(), Ip, default));
    }

    [Fact]
    public async Task AnUncastRoleIsAnAbsentCreditRatherThanABlankOne()
    {
        // The pages render "Nobody yet" from a null; an empty-but-present credit would link to
        // user 0 and show a broken avatar instead.
        var quest = await QuestService().GetAsync(QuestName, default);

        Assert.Null(quest!.Writer);
        Assert.Null(quest.Npcs.Single(npc => npc.NpcName == "Broadcast").VoiceActor);
        Assert.NotNull(quest.Npcs.Single(npc => npc.NpcName == "Captain Ackbar").VoiceActor);
    }

    [Fact]
    public async Task AVisitorsVotesOnAQuestComeBackSplitByDirection()
    {
        var votes = new FakeVotes { [1] = VoteType.Up, [CastNpcId] = VoteType.Down };

        var result = await QuestService(votes: votes).GetVotesAsync(QuestName, Anonymous(), Ip, default);

        Assert.Equal([1], result!.Upvoted);
        Assert.Equal([CastNpcId], result.Downvoted);
    }

    [Fact]
    public async Task AQuestOnlyAsksForVotesOnItsOwnNpcs()
    {
        // The vote lookup is keyed on the caller, so asking about NPCs the page does not show
        // would leak work - and, on a shared address, other pages' highlights.
        var votes = new FakeVotes();

        await QuestService(votes: votes).GetVotesAsync(QuestName, Anonymous(), Ip, default);

        Assert.Equal([1, CastNpcId], votes.LastRequested);
    }

    [Fact]
    public async Task AnNpcCarriesItsPortraitAndAFallbackForWhenThereIsNone()
    {
        // Portraits are addressed by id and never checked for existence, so the client needs the
        // placeholder handed to it rather than guessing at the storage layout.
        var npc = await NpcService().GetAsync(CastNpcId, default);

        Assert.Equal($"https://storage.test/npcs/{CastNpcId}.webp", npc!.ImageUrl);
        Assert.Equal("https://storage.test/npcs/default.webp", npc.DefaultImageUrl);
    }

    [Fact]
    public async Task AnNpcListsEveryQuestItIsCastInEvenOneItHasNotRecordedFor()
    {
        var npc = await NpcService().GetAsync(CastNpcId, default);

        Assert.Equal(["Flight in Distress", "Silent Role"], npc!.Quests.Select(q => q.QuestName));
        Assert.Equal("kmaxi", npc.Quests.First().SoundEditor!.DisplayName);
        Assert.Null(npc.Quests.Last().SoundEditor);
    }

    [Fact]
    public async Task AnUnknownNpcIsNullForBothTheProfileAndTheVote()
    {
        Assert.Null(await NpcService().GetAsync(404, default));
        Assert.Null(await NpcService().GetVoteAsync(404, Anonymous(), Ip, default));
    }

    [Fact]
    public async Task AnNpcNobodyHasVotedOnReportsNoVoteRatherThanFailing()
    {
        var mine = await NpcService().GetVoteAsync(CastNpcId, Anonymous(), Ip, default);
        var voted = await NpcService(new FakeVotes { [CastNpcId] = VoteType.Down })
            .GetVoteAsync(CastNpcId, Anonymous(), Ip, default);

        Assert.Null(mine!.MyVote);
        Assert.Equal(VoteType.Down, voted!.MyVote);
    }

    [Fact]
    public async Task TheNpcIndexCarriesThePortraitsAndQuestsACardNeeds()
    {
        // The index is not about any one quest, so each character has to say where they come from
        // and which portrait to draw - the card is shared with the quest and cast pages.
        var page = await NpcService().ListAsync(new NpcSearchRequest(), default);

        var ackbar = page.Results.Single(npc => npc.NpcName == "Captain Ackbar");
        Assert.Equal($"https://storage.test/npcs/{CastNpcId}.webp", ackbar.ImageUrl);
        Assert.Equal("https://storage.test/npcs/default.webp", ackbar.DefaultImageUrl);
        Assert.Equal(["Flight in Distress", "Silent Role"], ackbar.Quests.Select(quest => quest.QuestName));
        Assert.Equal("puppy", ackbar.VoiceActor!.DisplayName);
        Assert.Null(page.Results.Single(npc => npc.NpcName == "Broadcast").VoiceActor);
    }

    [Fact]
    public async Task ABlankSearchAsksForEveryNpcRatherThanForTheEmptyString()
    {
        // The box is cleared by deleting its text, which arrives as "" or whitespace; treating
        // that as a term would match nothing and empty the index.
        var repository = new FakeContentPages();

        await NpcService(repository: repository).ListAsync(new NpcSearchRequest("   "), default);

        Assert.Null(repository.LastNpcCriteria!.Search);
    }

    [Fact]
    public async Task ASearchIsTrimmedAndNarrowsTheIndex()
    {
        var page = await NpcService().ListAsync(new NpcSearchRequest(" ackbar "), default);

        Assert.Equal(["Captain Ackbar"], page.Results.Select(npc => npc.NpcName));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task AnOutOfRangePageSizeIsClampedRatherThanPassedToTheQuery()
    {
        // Page and size come off the query string, and the size becomes a LIMIT.
        var repository = new FakeContentPages();

        await NpcService(repository: repository).ListAsync(new NpcSearchRequest(Page: 0, PageSize: 5000), default);

        Assert.Equal(1, repository.LastNpcCriteria!.Page);
        Assert.Equal(100, repository.LastNpcCriteria.PageSize);
    }

    [Fact]
    public async Task AListingAsksForVotesOnlyOnTheNpcsItShows()
    {
        var votes = new FakeVotes { [CastNpcId] = VoteType.Up, [1] = VoteType.Down };

        var result = await NpcService(votes).GetVotesAsync([CastNpcId, CastNpcId, 1], Anonymous(), Ip, default);

        // Repeated ids come from pages overlapping; the lookup builds an IN list from them.
        Assert.Equal([CastNpcId, 1], votes.LastRequested);
        Assert.Equal([CastNpcId], result.Upvoted);
        Assert.Equal([1], result.Downvoted);
    }

    [Fact]
    public async Task AskingAboutNoNpcsAnswersEmptyWithoutTouchingTheVoteLookup()
    {
        var votes = new FakeVotes();

        var result = await NpcService(votes).GetVotesAsync([], Anonymous(), Ip, default);

        Assert.Empty(result.Upvoted);
        Assert.Empty(result.Downvoted);
        Assert.Empty(votes.LastRequested);
    }

    private const string Ip = "203.0.113.5";

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static QuestPageService QuestService(string[]? scripts = null, FakeVotes? votes = null)
    {
        var storage = new FakeScriptStorage(scripts ?? []);
        return new QuestPageService(
            new FakeContentPages(),
            new FakeImageStorage(),
            storage,
            new QuestScriptCatalog(storage, new MemoryCache(new MemoryCacheOptions())),
            votes ?? new FakeVotes());
    }

    private static NpcPageService NpcService(FakeVotes? votes = null, FakeContentPages? repository = null) => new(
        repository ?? new FakeContentPages(),
        new MemoryNpcs(),
        new FakeImageStorage(),
        votes ?? new FakeVotes());
}

/// <summary>One quest with two NPCs, one of them uncast, plus an NPC with a silent second quest.</summary>
internal sealed class FakeContentPages : IContentPageRepository
{
    private static readonly ContentCredit Puppy = new(
        232, "puppy", "https://storage.test/avatars/232.png", "https://storage.test/avatars/default.png");

    private static readonly ContentCredit Kmaxi = new(
        7, "kmaxi", "https://storage.test/avatars/7.png", "https://storage.test/avatars/default.png");

    public Task<IReadOnlyCollection<QuestListItem>> GetQuestListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<QuestListItem>>(
            [new QuestListItem(1, "Flight in Distress", "flightindistress", 2, 38)]);

    public Task<QuestDetail?> GetQuestAsync(string degeneratedName, CancellationToken cancellationToken) =>
        Task.FromResult(degeneratedName == "flightindistress"
            ? new QuestDetail(1, "Flight in Distress", "flightindistress", null, [
                new QuestNpc(1, "Broadcast", false, 0, 0, 0, 2, null, null),
                new QuestNpc(ContentPageTests.CastNpcId, "Captain Ackbar", false, 3, 1, 4, 36, Puppy, Kmaxi)])
            : null);

    public Task<IReadOnlyCollection<int>?> GetQuestNpcIdsAsync(
        string degeneratedName,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<int>?>(degeneratedName == "flightindistress"
            ? [1, ContentPageTests.CastNpcId]
            : null);

    public Task<NpcDetail?> GetNpcAsync(int npcId, CancellationToken cancellationToken) =>
        Task.FromResult(npcId == ContentPageTests.CastNpcId
            ? new NpcDetail(ContentPageTests.CastNpcId, "Captain Ackbar", false, 3, 1, 4, 36, Puppy, [
                new NpcQuestCredit(1, "Flight in Distress", "flightindistress", Kmaxi),
                new NpcQuestCredit(9, "Silent Role", "silentrole", null)])
            : null);

    /// <summary>Records what the service asked for, so the tests can assert on the narrowing.</summary>
    public NpcListCriteria? LastNpcCriteria { get; private set; }

    public Task<NpcListPage> GetNpcListAsync(NpcListCriteria criteria, CancellationToken cancellationToken)
    {
        LastNpcCriteria = criteria;
        NpcListItem[] all =
        [
            new(1, "Broadcast", false, 0, 0, 0, 2, null, []),
            new(ContentPageTests.CastNpcId, "Captain Ackbar", false, 3, 1, 4, 36, Puppy, [
                new NpcQuestAppearance(1, "Flight in Distress", "flightindistress"),
                new NpcQuestAppearance(9, "Silent Role", "silentrole")]),
        ];

        var matches = criteria.Search is null
            ? all
            : all.Where(npc => npc.NpcName.Contains(criteria.Search, StringComparison.OrdinalIgnoreCase)).ToArray();

        return Task.FromResult(new NpcListPage(
            matches.Length,
            criteria.Page,
            criteria.PageSize,
            matches.Skip((criteria.Page - 1) * criteria.PageSize).Take(criteria.PageSize).ToArray()));
    }
}

internal sealed class FakeImageStorage : INpcImageStorage
{
    public Uri GetImageUrl(int npcId) => new($"https://storage.test/npcs/{npcId}.webp");

    public Uri GetDefaultImageUrl() => new("https://storage.test/npcs/default.webp");

    public Task UploadImageAsync(int npcId, Stream webpContent, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> CopyImageIfExistsAsync(int source, int destination, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlySet<int>> ListNpcIdsWithImagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(NpcIdsWithImages);

    public HashSet<int> NpcIdsWithImages { get; } = [];
}

internal sealed class FakeScriptStorage(string[] names) : IQuestScriptStorage
{
    public Task<IReadOnlySet<string>> ListScriptNamesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(names.ToHashSet(StringComparer.Ordinal));

    public Task<bool> ScriptExistsAsync(string degeneratedName, CancellationToken cancellationToken) =>
        Task.FromResult(names.Contains(degeneratedName));

    public Uri GetScriptUrl(string degeneratedName) =>
        new($"https://storage.test/scripts/{degeneratedName}.txt");

    public Task UploadScriptAsync(string degeneratedName, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class FakeVotes : INpcVoteService
{
    private readonly Dictionary<int, VoteType> votes = [];

    public IReadOnlyCollection<int> LastRequested { get; private set; } = [];

    public VoteType this[int npcId]
    {
        set => votes[npcId] = value;
    }

    public Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        LastRequested = npcIds;
        return Task.FromResult<IReadOnlyDictionary<int, VoteType>>(votes
            .Where(vote => npcIds.Contains(vote.Key))
            .ToDictionary(vote => vote.Key, vote => vote.Value));
    }

    public Task<NpcWriteResult<NpcVoteResponse>> SetVoteAsync(
        int npcId,
        VoteType vote,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<NpcWriteResult<NpcVoteResponse>> ClearVoteAsync(
        int npcId,
        ClaimsPrincipal user,
        string ipAddress,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
