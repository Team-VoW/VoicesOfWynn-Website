using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Domain.Auth;
using VoW.Api.Repositories;
using VoW.Api.Services.Feedback;
using Xunit;

namespace VoW.Api.Tests;

public sealed class FeedbackTests
{
    private const string Secret = "test-only-quest-feedback-secret-with-32-bytes";
    private static QuestRatingRequest Rating(int score = 3) => new(Guid.NewGuid(), Guid.NewGuid(), "Recover the Past", score, "v2.2.0");
    private static QuestFeedbackService Service(MemoryFeedback db) => new(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["QUEST_FEEDBACK_EDIT_SECRET"] = Secret }).Build());

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("short")]
    public async Task MissingSecretDoesNotConsumeRetriesAndRecoveryAcceptsSameSubmission(string? secret)
    {
        var db = new MemoryFeedback();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["QUEST_FEEDBACK_EDIT_SECRET"] = secret }).Build();
        var service = new QuestFeedbackService(db, configuration);
        var request = Rating();
        for (var i = 0; i < 40; i++) Assert.Equal(503, (await service.RateAsync(request, "::1", default)).Status);
        Assert.Empty(db.Ratings);
        configuration["QUEST_FEEDBACK_EDIT_SECRET"] = Secret;
        Assert.Equal(200, (await service.RateAsync(request, "::1", default)).Status);
        Assert.Single(db.Ratings);
    }

    [Fact] public async Task MissingSecretReturns503WithRetryGuidance()
    {
        await using var app = new FeedbackApp("");
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/feedback/quests", Rating());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter!.Delta);
        Assert.DoesNotContain("QUEST_FEEDBACK_EDIT_SECRET", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task EveryScoreAndLostResponseRetries(int score)
    {
        var db = new MemoryFeedback(); var service = Service(db); var request = Rating(score);
        var first = await service.RateAsync(request, "::1", default);
        var retry = await service.RateAsync(request, "::1", default);
        Assert.Equal(200, first.Status); Assert.Equal(first, retry); Assert.Single(db.Ratings);
        Assert.Equal(32, db.Ratings.Values.Single().EditTokenHash.Length);
        Assert.NotEqual(Convert.ToHexString(db.Ratings.Values.Single().EditTokenHash), first.Rating!.EditToken);
    }
    [Fact] public async Task ConflictingSubmissionCannotReplaceRatingOrRetrieveToken()
    {
        var db = new MemoryFeedback(); var service = Service(db); var request = Rating();
        await service.RateAsync(request, "::1", default);
        foreach (var conflict in new[] { request with { Score = 4 }, request with { QuestName = "Other" },
            request with { InstallationId = Guid.NewGuid() }, request with { ModVersion = "other" } })
            Assert.Equal(409, (await service.RateAsync(conflict, "::1", default)).Status);
        Assert.Single(db.Ratings);
    }
    [Fact] public async Task CommentRequiresTokenAndRatingSurvivesCancellation()
    {
        var db = new MemoryFeedback(); var service = Service(db); var request = Rating();
        var result = await service.RateAsync(request, "::1", default);
        Assert.Empty(db.Comments); Assert.Single(db.Ratings); // Screen cancellation sends no comment request.
        Assert.Equal(403, (await service.CommentAsync(request.SubmissionId, new(new string('x', 64), "Comment"), "::1", default)).Status);
        Assert.Equal(400, (await service.CommentAsync(request.SubmissionId, new(result.Rating!.EditToken, " "), "::1", default)).Status);
        Assert.Equal(400, (await service.CommentAsync(request.SubmissionId, new(result.Rating.EditToken, new string('x', 2001)), "::1", default)).Status);
        var comment = new QuestCommentRequest(result.Rating.EditToken, "<script>alert(1)</script>\nMore feedback");
        Assert.Equal(204, (await service.CommentAsync(request.SubmissionId, comment, "::1", default)).Status);
        Assert.Equal(204, (await service.CommentAsync(request.SubmissionId, comment, "::1", default)).Status);
        Assert.Single(db.Comments); Assert.Equal(comment.Comment, db.Comments[request.SubmissionId.ToString()]);
    }
    [Fact] public async Task InstallationAndIpEachLimitWrites()
    {
        var db = new MemoryFeedback(); var service = Service(db); var installation = Guid.NewGuid();
        for (var i = 0; i < 30; i++) Assert.Equal(200, (await service.RateAsync(Rating() with { InstallationId = installation }, "::1", default)).Status);
        Assert.Equal(429, (await service.RateAsync(Rating() with { InstallationId = installation }, "::2", default)).Status);
        db = new(); service = Service(db);
        for (var i = 0; i < 120; i++) Assert.Equal(200, (await service.RateAsync(Rating(), "::1", default)).Status);
        Assert.Equal(429, (await service.RateAsync(Rating(), "::1", default)).Status);
    }
    [Theory]
    [InlineData("§a Recover   the\tPast ", "RECOVER THE PAST")]
    [InlineData("King’s Recruit", "KING’S RECRUIT")]
    [InlineData("Café", "CAFÉ")]
    public void NormalizationPreservesPunctuationAndAccents(string name, string expected) => Assert.Equal(expected, QuestNameNormalizer.Normalize(name));

    [Fact] public async Task HttpValidationAndStaffPermissions()
    {
        await using var app = new FeedbackApp(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/admin/feedback/quests")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/feedback/quests")).StatusCode);
        foreach (var invalid in new[] { Rating(0), Rating(6), Rating() with { SubmissionId = Guid.Empty },
            Rating() with { InstallationId = Guid.Empty }, Rating() with { QuestName = " " },
            Rating() with { QuestName = new string('x', 201) }, Rating() with { ModVersion = new string('x', 65) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/feedback/quests", invalid)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt(false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/feedback/quests")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jwt(true));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/feedback/quests")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/feedback/quests/detail?key=RECOVER%20THE%20PAST")).StatusCode);
        foreach (var query in new[] { "page=0", "pageSize=101", "minimumCount=0", "sort=bad", "from=2026-09-05&to=2026-09-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/admin/feedback/quests?" + query)).StatusCode);
        var json = await client.GetStringAsync("/admin/feedback/quests");
        Assert.DoesNotContain("installation", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task HttpRateLimitReturnsRetryGuidance()
    {
        await using var app = new FeedbackApp(); using var client = app.CreateClient();
        var request = Rating();
        for (var i = 0; i < 30; i++) Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/feedback/quests", request)).StatusCode);
        var limited = await client.PostAsJsonAsync("/feedback/quests", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }
    private static string Jwt(bool reports) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims: reports ? new[] { new Claim("type", "access"), new Claim(CapabilityMapper.ClaimType, "reports.view") } : new[] { new Claim("type", "access") },
        expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

    private sealed class FeedbackApp(string editSecret = Secret) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JWT_SECRET", Secret);
            builder.UseSetting("QUEST_FEEDBACK_EDIT_SECRET", editSecret);
            builder.ConfigureServices(services => { services.RemoveAll<IQuestFeedbackRepository>(); services.AddSingleton<IQuestFeedbackRepository>(new MemoryFeedback()); });
        }
    }
}

internal sealed class MemoryFeedback : IQuestFeedbackRepository
{
    public readonly Dictionary<string, StoredQuestRating> Ratings = new();
    public readonly Dictionary<string, string> Comments = new();
    private readonly Dictionary<string, int> limits = new();
    public Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct)
    { var k = Convert.ToHexString(key); limits.TryGetValue(k, out var n); limits[k] = n + 1; return Task.FromResult(n < limit); }
    public Task<StoredQuestRating?> FindAsync(string id, CancellationToken ct) => Task.FromResult(Ratings.GetValueOrDefault(id));
    public Task InsertAsync(QuestRatingRequest r, string key, byte[] hash, CancellationToken ct)
    { Ratings.TryAdd(r.SubmissionId.ToString(), new() { SubmissionId = r.SubmissionId.ToString(), InstallationId = r.InstallationId.ToString(), QuestName = r.QuestName, Score = r.Score, ModVersion = r.ModVersion, EditTokenHash = hash }); return Task.CompletedTask; }
    public Task UpdateCommentAsync(string id, string comment, CancellationToken ct) { Comments[id] = comment; return Task.CompletedTask; }
    public Task<FeedbackPage<QuestFeedbackSummary>> SummaryAsync(QuestFeedbackQuery q, CancellationToken ct) => Task.FromResult(new FeedbackPage<QuestFeedbackSummary>([], 0, q.Page, q.PageSize));
    public Task<QuestFeedbackDetail> DetailAsync(string key, QuestFeedbackQuery q, CancellationToken ct) => Task.FromResult(new QuestFeedbackDetail([], new([], 0, q.Page, q.PageSize)));
}
