using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Repositories;
using VoW.Api.Services.Feedback;
using Xunit;

namespace VoW.Api.Tests;

/// <summary>Opt-in tests: run only against the disposable Liquibase-migrated database.</summary>
public sealed class FeedbackDatabaseTests
{
    [DisposableDatabaseFact]
    public async Task RealMysqlMatchingAggregationPaginationAndConcurrentRetries()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["WEBSITE_DB_HOST"] = "127.0.0.1", ["WEBSITE_DB_PORT"] = "13316", ["WEBSITE_DB_NAME"] = "website",
            ["DB_USER"] = "root", ["DB_PASSWORD"] = "feedback-disposable-only",
            ["QUEST_FEEDBACK_EDIT_SECRET"] = "disposable-feedback-test-key-at-least-32-bytes"
        }).Build();
        await using var db = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(config));
        await db.OpenAsync();
        // The explicitly opted-in container is dedicated to this test; never use developer or production data.
        await db.ExecuteAsync("DELETE FROM quest_feedback; DELETE FROM quest_feedback_write_limit;");
        await db.ExecuteAsync("DELETE FROM quest WHERE degenerated_name IN ('feedback_test_later', 'feedback_test_ambiguous_1', 'feedback_test_ambiguous_2')");
        await db.ExecuteAsync("INSERT INTO quest (name, degenerated_name) VALUES ('Recover the Past', 'feedback_test_recover') ON DUPLICATE KEY UPDATE name=VALUES(name)");
        var repository = new QuestFeedbackRepository(config);
        var service = new QuestFeedbackService(repository, config);
        async Task<QuestRatingRequest> Rate(string name, int score)
        {
            var r = new QuestRatingRequest(Guid.NewGuid(), Guid.NewGuid(), name, score, "v2.2.0");
            Assert.Equal(200, (await service.RateAsync(r, "::1", default)).Status);
            return r;
        }
        var first = await Rate("§aRecover  the Past", 1);
        await Rate("recover the past", 5);
        await Rate("Never Catalogued", 2);
        await Rate("never  catalogued", 4);
        await Rate("Other Unknown", 3);
        var firstAccepted = await service.RateAsync(first, "::1", default);
        Assert.Equal(204, (await service.CommentAsync(first.SubmissionId, new(firstAccepted.Rating!.EditToken, "<b>Plain text</b>"), "::1", default)).Status);
        var summary = await repository.SummaryAsync(new(), default);
        Assert.Equal(3, summary.Total);
        Assert.All(summary.Items, item => Assert.Equal(3, item.AverageScore));
        Assert.Equal(2, summary.Items[0].RatingCount); Assert.Equal(1, summary.Items[^1].RatingCount);
        Assert.False(summary.Items.Single(x => x.GroupingKey == "RECOVER THE PAST").Unmatched);
        Assert.True(summary.Items.Single(x => x.GroupingKey == "NEVER CATALOGUED").Unmatched);
        Assert.Equal(1, summary.Items.Single(x => x.GroupingKey == "RECOVER THE PAST").CommentCount);
        var filtered = await repository.SummaryAsync(new() { Search = "never", MinimumCount = 2 }, default);
        Assert.Single(filtered.Items); Assert.Equal(2, filtered.Items[0].RatingCount);
        Assert.Empty((await repository.SummaryAsync(new() { MinimumCount = 3 }, default)).Items);
        var firstPage = await repository.SummaryAsync(new() { PageSize = 1 }, default);
        var secondPage = await repository.SummaryAsync(new() { PageSize = 1, Page = 2 }, default);
        Assert.NotEqual(firstPage.Items[0].GroupingKey, secondPage.Items[0].GroupingKey);
        var detail = await repository.DetailAsync("RECOVER THE PAST", new() { CommentsOnly = true }, default);
        Assert.Equal(2, detail.Distribution.Sum(x => x.Count)); Assert.Single(detail.Feedback.Items);
        Assert.Equal("<b>Plain text</b>", detail.Feedback.Items[0].Comment);
        Assert.Equal(DateTimeKind.Utc, detail.Feedback.Items[0].CreatedAt.Kind);
        var fullDetail = await repository.DetailAsync("RECOVER THE PAST", new() { PageSize = 1, Page = 2 }, default);
        Assert.Equal(2, fullDetail.Feedback.Total); Assert.Single(fullDetail.Feedback.Items);
        Assert.Empty((await repository.SummaryAsync(new() { To = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) }, default)).Items);
        Assert.Equal(3, (await repository.SummaryAsync(new() { From = DateOnly.FromDateTime(DateTime.UtcNow), To = DateOnly.FromDateTime(DateTime.UtcNow) }, default)).Total);
        await Rate("Lowest", 1); await Rate("Highest", 5);
        Assert.Equal("Lowest", (await repository.SummaryAsync(new(), default)).Items[0].QuestName);
        Assert.Equal("Highest", (await repository.SummaryAsync(new() { Sort = "highest" }, default)).Items[0].QuestName);
        // Concurrent HTTP retries exercise the real unique constraint rather than a test double.
        var concurrent = new QuestRatingRequest(Guid.NewGuid(), Guid.NewGuid(), "Concurrent", 3, "test");
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.RateAsync(concurrent, "::1", default)));
        Assert.All(responses, x => Assert.Equal(responses[0], x));
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM quest_feedback WHERE submission_id=@id", new { id = concurrent.SubmissionId.ToString() }));
        var key = System.Security.Cryptography.SHA256.HashData("test-limit"u8.ToArray());
        var limits = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => repository.ConsumeLimitAsync(key, 3, default)));
        Assert.Equal(3, limits.Count(x => x));
        await db.ExecuteAsync("INSERT INTO quest (name, degenerated_name) VALUES ('Ambiguous', 'feedback_test_ambiguous_1'), ('AMBIGUOUS', 'feedback_test_ambiguous_2') ON DUPLICATE KEY UPDATE name=VALUES(name)");
        await Rate("Ambiguous", 3);
        await Rate("Recover the Pas", 3);
        await Rate("Recover thé Past", 3);
        foreach (var name in new[] { "Ambiguous", "Recover the Pas", "Recover thé Past" })
            Assert.True((await repository.DetailAsync(QuestNameNormalizer.Normalize(name), new(), default)).Feedback.Items[0].Unmatched);
        // A later catalog entry must not hide the fact that earlier submissions were unmatched.
        await db.ExecuteAsync("INSERT INTO quest (name, degenerated_name) VALUES ('Never Catalogued', 'feedback_test_later') ON DUPLICATE KEY UPDATE name=VALUES(name)");
        await Rate("Never Catalogued", 5);
        var mixed = (await repository.SummaryAsync(new() { Search = "never" }, default)).Items.Single();
        Assert.Equal(2, mixed.UnmatchedCount);
        Assert.Equal(2, (await repository.DetailAsync(mixed.GroupingKey, new(), default)).Feedback.Items.Count(x => x.Unmatched));
    }
}

public sealed class DisposableDatabaseFactAttribute : FactAttribute
{
    public DisposableDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("QUEST_FEEDBACK_TEST_DB") != "disposable")
            Skip = "Requires the disposable database populated by Liquibase; see quest-feedback-rollout.md.";
    }
}
