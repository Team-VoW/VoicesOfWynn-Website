using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using VoW.Api.Contracts.Npcs;
using VoW.Api.Domain.Accounts;
using VoW.Api.Domain.Npcs;
using VoW.Api.Repositories;
using VoW.Api.Services.Npcs;
using Xunit;

namespace VoW.Api.Tests;

public sealed class NpcCommentTests
{
    private const int NpcId = 7;

    [Fact]
    public async Task SerializedCommentsCarryNoEmailAddressOrIpAddress()
    {
        var npcs = new MemoryNpcs();
        npcs.Comments.Add(new StoredComment(
            1, NpcId, UserId: null, Name: "Guest", Email: "someone@example.com", Ip: [127, 0, 0, 1],
            Content: "Great performance"));
        var service = Service(npcs);

        var response = await service.GetCommentsAsync(NpcId, Anonymous(), default);

        // Asserted on the serialized payload rather than the properties, so that adding a field
        // to the DTO later cannot quietly start publishing either value.
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("someone@example.com", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ip\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MentionsInACommentAreNeverHonouredByDiscord()
    {
        var npcs = new MemoryNpcs();
        var notifier = new RecordingNotifier();
        var service = Service(npcs, notifier);

        var result = await service.PostAsync(
            NpcId, new PostNpcCommentRequest("Guest", null, "@everyone look at this"), Anonymous(), "203.0.113.5", default);

        Assert.True(result.Succeeded);
        Assert.Single(notifier.Sent);
        Assert.Contains("@everyone", notifier.Sent[0].Content, StringComparison.Ordinal);
        // The notifier is what must neutralize it; see DiscordCommentNotifier's allowed_mentions.
        Assert.Contains("allowed_mentions", DiscordPayloadShape(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnauthenticatedPostIsStoredAsAGuestEvenWhenItClaimsOtherwise()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        var result = await service.PostAsync(
            NpcId, new PostNpcCommentRequest("Impersonator", null, "hello"), Anonymous(), "203.0.113.5", default);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.Verified);
        Assert.Null(npcs.Comments[0].UserId);
    }

    [Fact]
    public async Task ASignedInContributorPostsUnderTheirAccountAndNotTheSuppliedName()
    {
        var npcs = new MemoryNpcs();
        var service = Service(npcs);

        var result = await service.PostAsync(
            NpcId, new PostNpcCommentRequest("Not my name", "spoofed@example.com", "hello"), SignedIn(42), "203.0.113.5", default);

        Assert.True(result.Succeeded);
        Assert.Equal(42, npcs.Comments[0].UserId);
        Assert.Null(npcs.Comments[0].Name);
        Assert.Null(npcs.Comments[0].Email);
        // A contributor is identified by their account, so no address is recorded for them.
        Assert.Null(npcs.Comments[0].Ip);
    }

    [Theory]
    [InlineData("", null, "Content")]
    [InlineData("   ", null, "Content")]
    public async Task EmptyCommentsAreRejected(string content, string? name, string field)
    {
        var result = await Service(new MemoryNpcs())
            .PostAsync(NpcId, new PostNpcCommentRequest(name, null, content), Anonymous(), "203.0.113.5", default);

        Assert.False(result.Succeeded);
        Assert.Contains(field, result.Errors!.Keys);
    }

    [Fact]
    public async Task OversizedFieldsAreRejectedAtTheColumnWidths()
    {
        var service = Service(new MemoryNpcs());

        var longContent = await service.PostAsync(
            NpcId, new PostNpcCommentRequest(null, null, new string('a', 2001)), Anonymous(), "203.0.113.5", default);
        var longName = await service.PostAsync(
            NpcId, new PostNpcCommentRequest(new string('a', 32), null, "hi"), Anonymous(), "203.0.113.5", default);
        var badEmail = await service.PostAsync(
            NpcId, new PostNpcCommentRequest(null, "not-an-email", "hi"), Anonymous(), "203.0.113.5", default);

        Assert.False(longContent.Succeeded);
        Assert.False(longName.Succeeded);
        Assert.False(badEmail.Succeeded);
    }

    [Fact]
    public async Task ExactlyMaximumLengthFieldsAreAccepted()
    {
        var result = await Service(new MemoryNpcs()).PostAsync(
            NpcId,
            new PostNpcCommentRequest(new string('a', 31), null, new string('b', 2000)),
            Anonymous(),
            "203.0.113.5",
            default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task TheEleventhCommentFromOneVisitorInAnHourIsRateLimitedAndNotAnnounced()
    {
        var npcs = new MemoryNpcs();
        var notifier = new RecordingNotifier();
        var service = Service(npcs, notifier);

        for (var i = 0; i < 10; i++)
        {
            var allowed = await service.PostAsync(
                NpcId, new PostNpcCommentRequest(null, null, $"comment {i}"), Anonymous(), "203.0.113.5", default);
            Assert.True(allowed.Succeeded);
        }

        var blocked = await service.PostAsync(
            NpcId, new PostNpcCommentRequest(null, null, "one too many"), Anonymous(), "203.0.113.5", default);

        Assert.True(blocked.IsRateLimited);
        Assert.Equal(10, npcs.Comments.Count);
        Assert.Equal(10, notifier.Sent.Count);
    }

    [Fact]
    public async Task PostingToAnNpcThatDoesNotExistIsNotFound()
    {
        var result = await Service(new MemoryNpcs()).PostAsync(
            404, new PostNpcCommentRequest(null, null, "hi"), Anonymous(), "203.0.113.5", default);

        Assert.False(result.Succeeded);
        Assert.False(result.Found);
    }

    [Fact]
    public async Task TheAuthorCanDeleteTheirOwnComment()
    {
        var npcs = Commented();

        var result = await Service(npcs).DeleteAsync(NpcId, 1, SignedIn(42), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ASystemAdminCanDeleteAnyComment()
    {
        var npcs = Commented();

        var result = await Service(npcs, admins: [9]).DeleteAsync(NpcId, 2, SignedIn(9), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task AnotherContributorCannotDeleteSomeoneElsesComment()
    {
        var npcs = Commented();

        var result = await Service(npcs).DeleteAsync(NpcId, 1, SignedIn(43), default);

        Assert.True(result.IsForbidden);
        Assert.Equal(2, npcs.Comments.Count);
    }

    [Fact]
    public async Task AVisitorSharingTheStoredAddressStillCannotDeleteAGuestComment()
    {
        // The legacy site allowed this. Behind the reverse proxy every guest shares one address,
        // so the rule would let any visitor delete any other guest's comment.
        var npcs = Commented();

        var result = await Service(npcs).DeleteAsync(NpcId, 2, Anonymous(), default);

        Assert.True(result.IsForbidden);
        Assert.Equal(2, npcs.Comments.Count);
    }

    private static MemoryNpcs Commented()
    {
        var npcs = new MemoryNpcs();
        npcs.Comments.Add(new StoredComment(1, NpcId, UserId: 42, Name: null, Email: null, Ip: null, Content: "mine"));
        npcs.Comments.Add(new StoredComment(
            2, NpcId, UserId: null, Name: "Guest", Email: null, Ip: [203, 0, 113, 5], Content: "guest"));
        return npcs;
    }

    [Fact]
    public async Task DeletingACommentThatBelongsToAnotherNpcIsNotFound()
    {
        var npcs = new MemoryNpcs();
        npcs.Comments.Add(new StoredComment(1, NpcId: 99, UserId: 42, Name: null, Email: null, Ip: null, Content: "elsewhere"));

        var result = await Service(npcs).DeleteAsync(NpcId, 1, SignedIn(42), default);

        Assert.False(result.Found);
    }

    private static string DiscordPayloadShape() =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src/VoW.Api/Services/Npcs/DiscordCommentNotifier.cs"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VoW.Api.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the API project root.");
    }

    private static NpcCommentService Service(
        MemoryNpcs npcs,
        ICommentNotifier? notifier = null,
        int[]? admins = null) =>
        new(npcs, new MemoryAccounts(admins ?? []), new MemoryWriteLimits(), notifier ?? new RecordingNotifier());

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(int userId) =>
        new(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "test"));
}

internal sealed record StoredComment(
    int CommentId,
    int NpcId,
    int? UserId,
    string? Name,
    string? Email,
    byte[]? Ip,
    string Content);

internal sealed class RecordingNotifier : ICommentNotifier
{
    public List<CommentNotification> Sent { get; } = [];

    public Task NotifyAsync(CommentNotification notification, CancellationToken cancellationToken)
    {
        Sent.Add(notification);
        return Task.CompletedTask;
    }
}

internal sealed class MemoryWriteLimits : IWriteLimitRepository
{
    private readonly Dictionary<string, int> writes = [];

    public Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct)
    {
        var id = Convert.ToHexString(key);
        writes[id] = writes.GetValueOrDefault(id) + 1;
        return Task.FromResult(writes[id] <= limit);
    }
}

internal sealed class MemoryAccounts(int[] systemAdmins) : IAccountRepository
{
    public Task<bool> IsSystemAdminAsync(int userId, CancellationToken cancellationToken) =>
        Task.FromResult(systemAdmins.Contains(userId));

    public Task<IReadOnlyCollection<AccountRole>> GetRolesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AccountSearchPage> SearchAsync(AccountSearchCriteria criteria, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AccountDetails?> GetAsync(int userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> DisplayNameExistsAsync(int exceptUserId, string displayName, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> EmailExistsAsync(int exceptUserId, string email, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> DiscordIdExistsAsync(int exceptUserId, string discordId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> SocialExistsAsync(int exceptUserId, string column, string value, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> UpdateAsync(int userId, UpdateAccountCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> UpdateSelfProfileAsync(int userId, UpdateSelfProfileCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AccountPasswordState?> GetPasswordStateAsync(int userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> SetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<int> InsertAsync(CreateAccountCommand command, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> SetAvatarAsync(int userId, string picture, PictureType pictureType, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ClearAvatarAsync(int userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ResetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class MemoryNpcs : INpcInteractionRepository
{
    private const int KnownNpcId = 7;

    public List<StoredComment> Comments { get; } = [];

    public Dictionary<(int NpcId, string Voter), VoteType> Votes { get; } = [];

    public Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken) =>
        Task.FromResult(npcId == KnownNpcId);

    public Task<string?> GetNpcNameAsync(int npcId, CancellationToken cancellationToken) =>
        Task.FromResult(npcId == KnownNpcId ? "Aledar" : null);

    public Task<IReadOnlyCollection<QuestRecording>> GetRecordingsAsync(int npcId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<QuestRecording>>([]);

    public Task<IReadOnlyDictionary<int, QuestName>> GetQuestNamesAsync(
        IReadOnlyCollection<int> questIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, QuestName>>(new Dictionary<int, QuestName>());

    public Task<NpcVoteCounts> SetVoteAsync(int npcId, string voterId, VoteType vote, CancellationToken cancellationToken)
    {
        Votes[(npcId, voterId)] = vote;
        return Task.FromResult(Counts(npcId));
    }

    public Task<NpcVoteCounts> ClearVoteAsync(int npcId, string voterId, CancellationToken cancellationToken)
    {
        Votes.Remove((npcId, voterId));
        return Task.FromResult(Counts(npcId));
    }

    public Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        string voterId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, VoteType>>(Votes
            .Where(vote => vote.Key.Voter == voterId && npcIds.Contains(vote.Key.NpcId))
            .ToDictionary(vote => vote.Key.NpcId, vote => vote.Value));

    public Task<IReadOnlyCollection<NpcComment>> GetCommentsAsync(int npcId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<NpcComment>>(Comments
            .Where(comment => comment.NpcId == npcId)
            .Select(Project)
            .ToArray());

    public Task<int> InsertCommentAsync(NewNpcComment comment, CancellationToken cancellationToken)
    {
        var id = Comments.Count + 1;
        Comments.Add(new StoredComment(
            id, comment.NpcId, comment.UserId, comment.Name, comment.Email, comment.Ip, comment.Content));
        return Task.FromResult(id);
    }

    public Task<NpcComment?> GetCommentAsync(int commentId, CancellationToken cancellationToken) =>
        Task.FromResult(Comments
            .Where(comment => comment.CommentId == commentId)
            .Select(Project)
            .FirstOrDefault());

    public Task<NpcCommentOwner?> GetCommentOwnerAsync(int commentId, CancellationToken cancellationToken)
    {
        var comment = Comments.FirstOrDefault(c => c.CommentId == commentId);
        return Task.FromResult(comment is null
            ? null
            : new NpcCommentOwner(comment.CommentId, comment.NpcId, comment.UserId));
    }

    public Task<bool> DeleteCommentAsync(int commentId, CancellationToken cancellationToken) =>
        Task.FromResult(Comments.RemoveAll(comment => comment.CommentId == commentId) > 0);

    private NpcVoteCounts Counts(int npcId) => new(
        Votes.Count(vote => vote.Key.NpcId == npcId && vote.Value == VoteType.Up),
        Votes.Count(vote => vote.Key.NpcId == npcId && vote.Value == VoteType.Down));

    // Mirrors the real repository, which never projects the e-mail address or IP into the model.
    private static NpcComment Project(StoredComment comment) => new(
        comment.CommentId,
        comment.UserId is not null,
        comment.UserId,
        comment.UserId is not null ? "Contributor" : comment.Name ?? "Anonymous",
        "https://www.gravatar.com/avatar/abc?d=identicon",
        comment.Content,
        DateTime.UtcNow);
}
