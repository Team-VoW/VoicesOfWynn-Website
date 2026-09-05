using Dapper;
using MySqlConnector;
using VoW.Api.Contracts.Feedback;
using VoW.Api.Services.Feedback;

namespace VoW.Api.Repositories;

public sealed class StoredQuestRating
{
    public string SubmissionId { get; set; } = "";
    public string InstallationId { get; set; } = "";
    public string QuestName { get; set; } = "";
    public int Score { get; set; }
    public string ModVersion { get; set; } = "";
    public byte[] EditTokenHash { get; set; } = [];
}

public sealed class QuestFeedbackRepository(IConfiguration configuration) : IQuestFeedbackRepository
{
    private MySqlConnection Connect() => new(new MySqlConnectionStringBuilder(DatabaseSettings.GetWebsiteConnectionString(configuration))
    {
        // These identifiers are stored and compared as canonical strings, not MySqlConnector's automatic CHAR(36) GUIDs.
        GuidFormat = MySqlGuidFormat.None
    }.ConnectionString);

    public async Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct)
    {
        await using var db = Connect();
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var window = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var args = new { key, window };
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO quest_feedback_write_limit VALUES (@key, @window, 1)
            ON DUPLICATE KEY UPDATE writes = writes + 1;
            """, args, tx, cancellationToken: ct));
        var count = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT writes FROM quest_feedback_write_limit WHERE bucket_key=@key AND window_start=@window", args, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM quest_feedback_write_limit WHERE window_start < @old LIMIT 1000",
            new { old = window.AddHours(-2) }, cancellationToken: ct));
        return count <= limit;
    }

    public async Task<StoredQuestRating?> FindAsync(string id, CancellationToken ct)
    {
        await using var db = Connect();
        return await db.QuerySingleOrDefaultAsync<StoredQuestRating>(new CommandDefinition("""
            SELECT submission_id SubmissionId, installation_id InstallationId, original_quest_name QuestName,
              score Score, mod_version ModVersion, edit_token_hash EditTokenHash
            FROM quest_feedback WHERE submission_id=@id
            """, new { id }, cancellationToken: ct));
    }

    public async Task InsertAsync(QuestRatingRequest request, string key, byte[] hash, CancellationToken ct)
    {
        await using var db = Connect();
        // Ambiguous catalog names remain unmatched. No fuzzy matching or accent-insensitive collation.
        var catalog = await db.QueryAsync<(int Id, string Name)>(new CommandDefinition(
            "SELECT quest_id, name FROM quest", cancellationToken: ct));
        var matches = catalog.Where(q => QuestNameNormalizer.Normalize(q.Name) == key).ToArray();
        try
        {
            await db.ExecuteAsync(new CommandDefinition("""
                INSERT INTO quest_feedback
                  (submission_id, installation_id, quest_id, original_quest_name, grouping_key, score,
                   mod_version, created_at, updated_at, edit_token_hash)
                VALUES (@id, @installation, @questId, @QuestName, @key, @Score, @ModVersion, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), @hash)
                """, new { id = request.SubmissionId.ToString(), installation = request.InstallationId.ToString(),
                    questId = matches.Length == 1 ? (int?)matches[0].Id : null, request.QuestName, key, request.Score, request.ModVersion, hash },
                cancellationToken: ct));
        }
        catch (MySqlException ex) when (ex.Number == 1062) { /* Concurrent retry: caller compares stored payload. */ }
    }

    public async Task UpdateCommentAsync(string id, string comment, CancellationToken ct)
    {
        await using var db = Connect();
        await db.ExecuteAsync(new CommandDefinition("""
            UPDATE quest_feedback SET updated_at=IF(comment <=> @comment, updated_at, UTC_TIMESTAMP(6)), comment=@comment
            WHERE submission_id=@id
            """, new { id, comment }, cancellationToken: ct));
    }

    private const string DateFilter = "(@From IS NULL OR f.created_at>=@From) AND (@Until IS NULL OR f.created_at<@Until)";
    private static object Args(QuestFeedbackQuery q, string? key = null) => new {
        From = q.From?.ToDateTime(TimeOnly.MinValue), Until = q.To?.ToDateTime(TimeOnly.MinValue).AddDays(1),
        Search = QuestNameNormalizer.Normalize(q.Search ?? ""), q.MinimumCount, q.PageSize,
        Offset = (q.Page - 1) * q.PageSize, key, q.CommentsOnly };

    public async Task<FeedbackPage<QuestFeedbackSummary>> SummaryAsync(QuestFeedbackQuery q, CancellationToken ct)
    {
        await using var db = Connect();
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var grouped = $"""
            SELECT f.grouping_key GroupingKey, COALESCE(MAX(q.name), MIN(f.original_quest_name)) QuestName,
              MAX(f.quest_id) IS NULL Unmatched, SUM(f.quest_id IS NULL) UnmatchedCount, AVG(f.score) AverageScore, COUNT(*) RatingCount,
              SUM(f.comment IS NOT NULL) CommentCount, MAX(f.created_at) LatestSubmission
            FROM quest_feedback f LEFT JOIN quest q ON f.quest_id=q.quest_id
            WHERE {DateFilter} AND (@Search='' OR LOCATE(@Search, f.grouping_key)>0)
            GROUP BY f.grouping_key HAVING COUNT(*)>=@MinimumCount
            """;
        var args = Args(q);
        var total = await db.ExecuteScalarAsync<long>(new CommandDefinition($"SELECT COUNT(*) FROM ({grouped}) grouped", args, tx, cancellationToken: ct));
        var direction = q.Sort == "highest" ? "DESC" : "ASC";
        var items = (await db.QueryAsync<QuestFeedbackSummary>(new CommandDefinition(
            $"{grouped} ORDER BY AverageScore {direction}, RatingCount DESC, GroupingKey ASC LIMIT @PageSize OFFSET @Offset", args, tx, cancellationToken: ct))).AsList();
        foreach (var item in items) item.LatestSubmission = DateTime.SpecifyKind(item.LatestSubmission, DateTimeKind.Utc);
        await tx.CommitAsync(ct);
        return new(items, total, q.Page, q.PageSize);
    }

    public async Task<QuestFeedbackDetail> DetailAsync(string key, QuestFeedbackQuery q, CancellationToken ct)
    {
        await using var db = Connect();
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var args = Args(q, key);
        var where = $"FROM quest_feedback f WHERE f.grouping_key=@key AND {DateFilter}";
        var distribution = (await db.QueryAsync<ScoreCount>(new CommandDefinition(
            $"SELECT CAST(score AS SIGNED) Score, COUNT(*) Count {where} GROUP BY score ORDER BY score", args, tx, cancellationToken: ct))).AsList();
        var filtered = $"{where} AND (@CommentsOnly=0 OR comment IS NOT NULL)";
        var total = await db.ExecuteScalarAsync<long>(new CommandDefinition($"SELECT COUNT(*) {filtered}", args, tx, cancellationToken: ct));
        var items = (await db.QueryAsync<QuestFeedbackItem>(new CommandDefinition(
            $"SELECT original_quest_name QuestName, quest_id IS NULL Unmatched, score Score, comment Comment, mod_version ModVersion, created_at CreatedAt {filtered} ORDER BY created_at DESC, submission_id LIMIT @PageSize OFFSET @Offset",
            args, tx, cancellationToken: ct))).AsList();
        foreach (var item in items) item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc);
        await tx.CommitAsync(ct);
        return new(distribution, new(items, total, q.Page, q.PageSize));
    }
}
