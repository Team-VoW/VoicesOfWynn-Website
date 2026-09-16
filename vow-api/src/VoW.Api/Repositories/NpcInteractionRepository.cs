using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Npcs;

namespace VoW.Api.Repositories;

public sealed class NpcInteractionRepository(IConfiguration configuration) : INpcInteractionRepository
{
    private readonly AvatarUrlResolver avatarUrls = new(configuration);

    private MySqlConnection Connect() => new(DatabaseSettings.GetWebsiteConnectionString(configuration));

    public async Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM npc WHERE npc_id = @NpcId;",
            new { NpcId = npcId },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<string?> GetNpcNameAsync(int npcId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT name FROM npc WHERE npc_id = @NpcId;",
            new { NpcId = npcId },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<QuestRecording>> GetRecordingsAsync(
        int npcId,
        CancellationToken cancellationToken)
    {
        // Archived recordings stay hidden unless the whole NPC is archived, in which case they
        // are all that is left of it. This mirrors the legacy site's visibility rule exactly.
        const string sql = """
            SELECT
                r.recording_id AS RecordingId,
                r.quest_id AS QuestId,
                r.line AS Line,
                r.file AS FileName
            FROM recording r
            JOIN npc n ON n.npc_id = r.npc_id
            WHERE r.npc_id = @NpcId AND (r.archived = FALSE OR n.archived = TRUE)
            ORDER BY r.quest_id, r.line;
            """;

        await using var connection = Connect();
        return (await connection.QueryAsync<QuestRecording>(
            new CommandDefinition(sql, new { NpcId = npcId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyDictionary<int, QuestName>> GetQuestNamesAsync(
        IReadOnlyCollection<int> questIds,
        CancellationToken cancellationToken)
    {
        if (questIds.Count == 0)
        {
            return new Dictionary<int, QuestName>();
        }

        const string sql = """
            SELECT quest_id AS QuestId, name AS Name, degenerated_name AS DegeneratedName
            FROM quest
            WHERE quest_id IN @QuestIds;
            """;

        await using var connection = Connect();
        var rows = await connection.QueryAsync<QuestName>(
            new CommandDefinition(sql, new { QuestIds = questIds }, cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.QuestId);
    }

    public async Task<NpcVoteCounts> SetVoteAsync(
        int npcId,
        string voterId,
        VoteType vote,
        CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The (npc_id, voter) unique key turns a repeat vote into a switch rather than a duplicate.
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO vote (npc_id, voter, type) VALUES (@NpcId, @Voter, @Type)
            ON DUPLICATE KEY UPDATE type = @Type;
            """,
            new { NpcId = npcId, Voter = voterId, Type = ToDatabaseValue(vote) },
            transaction,
            cancellationToken: cancellationToken));

        var counts = await RecountAsync(connection, transaction, npcId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return counts;
    }

    public async Task<NpcVoteCounts> ClearVoteAsync(int npcId, string voterId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM vote WHERE npc_id = @NpcId AND voter = @Voter;",
            new { NpcId = npcId, Voter = voterId },
            transaction,
            cancellationToken: cancellationToken));

        var counts = await RecountAsync(connection, transaction, npcId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return counts;
    }

    public async Task<IReadOnlyDictionary<int, VoteType>> GetVotesAsync(
        IReadOnlyCollection<int> npcIds,
        string voterId,
        CancellationToken cancellationToken)
    {
        if (npcIds.Count == 0)
        {
            return new Dictionary<int, VoteType>();
        }

        await using var connection = Connect();
        var rows = await connection.QueryAsync<VoteRow>(new CommandDefinition(
            "SELECT npc_id AS NpcId, type AS Type FROM vote WHERE voter = @Voter AND npc_id IN @NpcIds;",
            new { Voter = voterId, NpcIds = npcIds },
            cancellationToken: cancellationToken));

        var votes = new Dictionary<int, VoteType>();
        foreach (var row in rows)
        {
            var vote = ParseVote(row.Type);
            if (vote is not null)
            {
                votes[row.NpcId] = vote.Value;
            }
        }

        return votes;
    }

    // Selects no e-mail address and no IP: both are private, and the Gravatar they feed is hashed
    // in the query instead. Verified comments borrow the contributor's account avatar.
    private const string CommentSelect = """
        SELECT
            c.comment_id AS CommentId,
            c.verified AS Verified,
            c.user_id AS UserId,
            c.name AS Name,
            u.display_name AS DisplayName,
            u.picture AS Picture,
            u.picture_type AS PictureType,
            MD5(LOWER(TRIM(COALESCE(c.email, '')))) AS EmailHash,
            c.content AS Content,
            c.created_at AS CreatedAt
        FROM comment c
        LEFT JOIN user u ON u.user_id = c.user_id
        """;

    public async Task<IReadOnlyCollection<NpcComment>> GetCommentsAsync(
        int npcId,
        CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        var rows = (await connection.QueryAsync<CommentRow>(new CommandDefinition(
            $"{CommentSelect}\nWHERE c.npc_id = @NpcId\nORDER BY c.comment_id DESC;",
            new { NpcId = npcId },
            cancellationToken: cancellationToken))).AsList();

        return rows.Select(ToComment).ToArray();
    }

    public async Task<NpcComment?> GetCommentAsync(int commentId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        var row = await connection.QuerySingleOrDefaultAsync<CommentRow>(new CommandDefinition(
            $"{CommentSelect}\nWHERE c.comment_id = @CommentId;",
            new { CommentId = commentId },
            cancellationToken: cancellationToken));
        return row is null ? null : ToComment(row);
    }

    private NpcComment ToComment(CommentRow row) => new(
        row.CommentId,
        row.Verified != 0,
        row.UserId,
        AuthorName(row),
        AvatarUrl(row),
        row.Content,
        row.CreatedAt);

    public async Task<int> InsertCommentAsync(NewNpcComment comment, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO comment (verified, user_id, ip, name, email, content, npc_id, created_at)
            VALUES (@Verified, @UserId, @Ip, @Name, @Email, @Content, @NpcId, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = Connect();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new
            {
                Verified = comment.UserId is null ? 0 : 1,
                comment.UserId,
                comment.Ip,
                comment.Name,
                comment.Email,
                comment.Content,
                comment.NpcId,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<NpcCommentOwner?> GetCommentOwnerAsync(int commentId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        return await connection.QuerySingleOrDefaultAsync<NpcCommentOwner>(new CommandDefinition(
            "SELECT comment_id AS CommentId, npc_id AS NpcId, user_id AS UserId FROM comment WHERE comment_id = @CommentId;",
            new { CommentId = commentId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteCommentAsync(int commentId, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM comment WHERE comment_id = @CommentId;",
            new { CommentId = commentId },
            cancellationToken: cancellationToken)) > 0;
    }

    private static async Task<NpcVoteCounts> RecountAsync(
        MySqlConnection connection,
        System.Data.Common.DbTransaction transaction,
        int npcId,
        CancellationToken cancellationToken)
    {
        // npc.upvotes/downvotes are a denormalized cache of the vote table, recounted on write
        // so that listing NPCs never has to aggregate votes.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE npc SET
                upvotes = (SELECT COUNT(*) FROM vote WHERE npc_id = @NpcId AND type = '+'),
                downvotes = (SELECT COUNT(*) FROM vote WHERE npc_id = @NpcId AND type = '-')
            WHERE npc_id = @NpcId;
            """, new { NpcId = npcId }, transaction, cancellationToken: cancellationToken));

        return await connection.QuerySingleAsync<NpcVoteCounts>(new CommandDefinition(
            "SELECT upvotes AS Upvotes, downvotes AS Downvotes FROM npc WHERE npc_id = @NpcId;",
            new { NpcId = npcId },
            transaction,
            cancellationToken: cancellationToken));
    }

    private string? AvatarUrl(CommentRow row) =>
        row.UserId is not null && row.Picture is not null && row.PictureType is not null
            ? avatarUrls.AvatarUrl(row.Picture, row.PictureType)
            // Guests are identified by a Gravatar of their e-mail, as on the legacy site. An
            // empty address still hashes, which is what produces the shared default identicon.
            : $"https://www.gravatar.com/avatar/{row.EmailHash}?d=identicon";

    private static string AuthorName(CommentRow row) =>
        row.DisplayName ?? (string.IsNullOrWhiteSpace(row.Name) ? "Anonymous" : row.Name);

    private static string ToDatabaseValue(VoteType vote) => vote == VoteType.Up ? "+" : "-";

    private static VoteType? ParseVote(string? value) => value switch
    {
        "+" => VoteType.Up,
        "-" => VoteType.Down,
        _ => null,
    };

    private sealed class VoteRow
    {
        public int NpcId { get; set; }

        public string? Type { get; set; }
    }

    private sealed class CommentRow
    {
        public int CommentId { get; set; }

        public sbyte Verified { get; set; }

        public int? UserId { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Picture { get; set; }

        public string? PictureType { get; set; }

        public string EmailHash { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime? CreatedAt { get; set; }
    }
}
