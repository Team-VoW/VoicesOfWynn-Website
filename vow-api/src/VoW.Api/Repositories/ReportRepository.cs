using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public sealed class ReportRepository(IConfiguration configuration) : IReportRepository
{
    private static string ColumnFor(ReportSortField field) => field switch
    {
        ReportSortField.ReportId => "report_id",
        ReportSortField.NpcName => "npc_name",
        ReportSortField.ChatMessage => "chat_message",
        ReportSortField.Status => "status",
        ReportSortField.ReportedTimes => "reported_times",
        ReportSortField.TimeSubmitted => "time_submitted",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
    };

    public async Task<ReportSearchPage> SearchAsync(ReportSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (criteria.Npc is not null)
        {
            where.Add("npc_name LIKE @Npc");
            parameters.Add("Npc", $"%{criteria.Npc}%");
        }

        if (criteria.Content is not null)
        {
            where.Add("chat_message LIKE @Content");
            parameters.Add("Content", $"%{criteria.Content}%");
        }

        if (criteria.Status is not null)
        {
            where.Add("status = @Status");
            parameters.Add("Status", criteria.Status);
        }

        var whereSql = where.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", where)}";
        var offset = (criteria.Page - 1) * criteria.PageSize;
        parameters.Add("PageSize", criteria.PageSize);
        parameters.Add("Offset", offset);

        var orderBySql = BuildOrderBy(criteria);

        var countSql = $"SELECT COUNT(*) FROM report {whereSql};";
        var searchSql = $"""
            SELECT
                report_id AS ReportId,
                npc_name AS NpcName,
                chat_message AS ChatMessage,
                status AS Status,
                reported_times AS ReportedTimes,
                time_submitted AS TimeSubmitted
            FROM report
            {whereSql}
            {orderBySql}
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(command);

        command = new CommandDefinition(searchSql, parameters, cancellationToken: cancellationToken);
        var results = (await connection.QueryAsync<ReportSummary>(command)).AsList();

        return new ReportSearchPage(total, criteria.Page, results);
    }

    public async Task<bool> UpdateStatusAsync(int reportId, string status, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE report SET status = @Status WHERE report_id = @Id;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(sql, new { Id = reportId, Status = status }, cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<LineStatusUpsertCounts> UpsertLineStatusAsync(
        IReadOnlyList<string> chatMessages,
        string status,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        // Dapper expands the @Lines list into @Lines1..@LinesN and adds the parentheses itself,
        // so the SQL must not write them. Chunks are a fixed size because Dapper caches the
        // expanded SQL per parameter count.
        const string countSql = "SELECT COUNT(*) FROM report WHERE chat_message IN @Lines;";
        const string updateSql = "UPDATE report SET status = @Status WHERE chat_message IN @Lines;";
        const string insertSql = """
            INSERT IGNORE INTO report
                (chat_message, npc_name, player, pos_x, pos_y, pos_z, reported_times, status)
            VALUES
                (@ChatMessage, NULL, '<IMPORT>', NULL, NULL, NULL, 0, @Status);
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var updated = 0;
        var inserted = 0;

        for (var offset = 0; offset < chatMessages.Count; offset += chunkSize)
        {
            var chunk = Chunk(chatMessages, offset, chunkSize);

            // Counted before the UPDATE: an UPDATE's affected-rows reports only rows whose value
            // actually changed, so lines already at the target status would report 0 on a re-run.
            updated += await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(countSql, new { Lines = chunk }, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                updateSql, new { Lines = chunk, Status = status }, cancellationToken: cancellationToken));

            // INSERT IGNORE's affected-rows is unambiguous: ignored duplicates count 0.
            inserted += await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    chunk.Select(line => new { ChatMessage = line, Status = status }).ToArray(),
                    cancellationToken: cancellationToken));
        }

        // Final sweep. The mod client can insert a report row for one of these lines in the window
        // between a chunk's UPDATE and its INSERT IGNORE - the UPDATE missed it (it did not exist
        // yet) and the INSERT IGNORE dropped it (the unique index now hits), leaving it
        // 'unprocessed'. MyISAM has no transactions to close that window, so re-run the UPDATE.
        for (var offset = 0; offset < chatMessages.Count; offset += chunkSize)
        {
            var chunk = Chunk(chatMessages, offset, chunkSize);
            await connection.ExecuteAsync(new CommandDefinition(
                updateSql, new { Lines = chunk, Status = status }, cancellationToken: cancellationToken));
        }

        return new LineStatusUpsertCounts(updated, inserted);
    }

    public async Task<VoicedLineImportCounts> MarkLinesAsVoicedAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        var counts = await UpsertLineStatusAsync(chatMessages, "fixed", chunkSize, cancellationToken);
        return new VoicedLineImportCounts(counts.Updated, counts.Inserted);
    }

    public async Task<int> DeleteLinesAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM report WHERE chat_message IN @Lines;";

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var deleted = 0;
        for (var offset = 0; offset < chatMessages.Count; offset += chunkSize)
        {
            deleted += await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Lines = Chunk(chatMessages, offset, chunkSize) },
                cancellationToken: cancellationToken));
        }

        return deleted;
    }

    public async Task<LineReportOutcome> CreateOrIncrementAsync(
        NewLineReport report,
        CancellationToken cancellationToken)
    {
        // Insert first and let the unique index on chat_message decide whether this is a new line.
        // Looking it up first, as the PHP version did, leaves a window in which two players report
        // the same line at once: both see nothing, both insert, and the loser gets a 500.
        const string insertSql = """
            INSERT IGNORE INTO report (chat_message, npc_name, player, pos_x, pos_y, pos_z)
            VALUES (@ChatMessage, @NpcName, @PlayerName, @PosX, @PosY, @PosZ);
            """;

        // Coordinates converge on a running mean, and the placeholder values a script import
        // leaves behind (NULL npc/position, the literal <IMPORT> player) are filled in by the
        // first real sighting.
        const string incrementSql = """
            UPDATE report SET
                time_submitted = @Now,
                npc_name = COALESCE(npc_name, @NpcName),
                player = CASE WHEN player = '<IMPORT>' THEN @PlayerName ELSE player END,
                pos_x = CASE WHEN pos_x IS NULL THEN @PosX ELSE (pos_x * reported_times + COALESCE(@PosX, pos_x)) / (reported_times + 1) END,
                pos_y = CASE WHEN pos_y IS NULL THEN @PosY ELSE (pos_y * reported_times + COALESCE(@PosY, pos_y)) / (reported_times + 1) END,
                pos_z = CASE WHEN pos_z IS NULL THEN @PosZ ELSE (pos_z * reported_times + COALESCE(@PosZ, pos_z)) / (reported_times + 1) END,
                reported_times = reported_times + 1
            WHERE chat_message = @ChatMessage;
            """;

        const string countSql = "SELECT reported_times FROM report WHERE chat_message = @ChatMessage LIMIT 1;";

        var args = new
        {
            report.ChatMessage,
            report.NpcName,
            report.PlayerName,
            PosX = report.Position?.X,
            PosY = report.Position?.Y,
            PosZ = report.Position?.Z,
            Now = DateTime.UtcNow
        };

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var inserted = await connection.ExecuteAsync(
            new CommandDefinition(insertSql, args, cancellationToken: cancellationToken));
        if (inserted > 0)
        {
            return new LineReportOutcome(true, 1);
        }

        await connection.ExecuteAsync(new CommandDefinition(incrementSql, args, cancellationToken: cancellationToken));
        var reportedTimes = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { report.ChatMessage }, cancellationToken: cancellationToken));

        return new LineReportOutcome(false, reportedTimes);
    }

    public async Task<LineQueryPage> QueryLinesAsync(
        LineQueryCriteria criteria,
        CancellationToken cancellationToken)
    {
        var where = new List<string> { "status IN @Statuses", "reported_times >= @MinReports" };
        var parameters = new DynamicParameters();
        parameters.Add("Statuses", criteria.Statuses);
        parameters.Add("MinReports", criteria.MinReports);

        if (!string.IsNullOrWhiteSpace(criteria.Npc))
        {
            where.Add("npc_name = @Npc");
            parameters.Add("Npc", criteria.Npc);
        }

        if (criteria.Since is { } since)
        {
            where.Add("time_submitted >= @Since");
            parameters.Add("Since", since.ToDateTime(TimeOnly.MinValue));
        }

        parameters.Add("Limit", criteria.Limit);
        parameters.Add("Offset", criteria.Offset);

        var whereSql = $"WHERE {string.Join(" AND ", where)}";
        var countSql = $"SELECT COUNT(*) FROM report {whereSql};";

        // The PHP endpoints had no LIMIT at all: /valid with no filter serialised the whole table.
        var selectSql = $"""
            SELECT
                chat_message AS ChatMessage,
                npc_name AS NpcName,
                pos_x AS PosX,
                pos_y AS PosY,
                pos_z AS PosZ
            FROM report
            {whereSql}
            ORDER BY report_id ASC
            LIMIT @Limit OFFSET @Offset;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<LineDbRow>(
            new CommandDefinition(selectSql, parameters, cancellationToken: cancellationToken));

        return new LineQueryPage(total, rows.Select(row => new LineReportLine(
            row.ChatMessage,
            row.NpcName,
            row.PosX is null || row.PosY is null || row.PosZ is null
                ? null
                : new ReportPosition(row.PosX.Value, row.PosY.Value, row.PosZ.Value))).ToList());
    }

    private sealed record LineDbRow(string ChatMessage, string? NpcName, int? PosX, int? PosY, int? PosZ);

    public async Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM report WHERE report_id = @Id;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(sql, new { Id = reportId }, cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<IReadOnlyDictionary<string, ReportPosition>> GetLastPositionsByNpcNameAsync(
        IReadOnlyList<string> npcNames,
        CancellationToken cancellationToken)
    {
        if (npcNames.Count == 0)
        {
            return new Dictionary<string, ReportPosition>(StringComparer.OrdinalIgnoreCase);
        }

        // Names arrive already degenerated, which is the form report.npc_name stores.
        // ROW_NUMBER breaks ties on report_id; the legacy self-join on MAX(time_submitted) emitted
        // one row per report sharing that second, and the last one silently won.
        const string sql = """
            SELECT npc_name AS NpcName, pos_x AS X, pos_y AS Y, pos_z AS Z
            FROM (
                SELECT
                    npc_name, pos_x, pos_y, pos_z,
                    ROW_NUMBER() OVER (PARTITION BY npc_name ORDER BY time_submitted DESC, report_id DESC) AS rn
                FROM report
                WHERE npc_name IN @Names
                  AND pos_x IS NOT NULL AND pos_y IS NOT NULL AND pos_z IS NOT NULL
            ) ranked
            WHERE rn = 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var rows = await connection.QueryAsync<PositionDbRow>(new CommandDefinition(
            sql,
            new { Names = npcNames },
            cancellationToken: cancellationToken));

        var positions = new Dictionary<string, ReportPosition>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            positions[row.NpcName] = new ReportPosition(row.X, row.Y, row.Z);
        }

        return positions;
    }

    private sealed record PositionDbRow(string NpcName, int X, int Y, int Z);

    private static string[] Chunk(IReadOnlyList<string> source, int offset, int size)
    {
        var length = Math.Min(size, source.Count - offset);
        var chunk = new string[length];
        for (var i = 0; i < length; i++)
        {
            chunk[i] = source[offset + i];
        }

        return chunk;
    }

    private static string BuildOrderBy(ReportSearchCriteria criteria)
    {
        if (criteria.SortBy is not { } sortBy)
        {
            return "ORDER BY time_submitted DESC, report_id DESC";
        }

        var direction = criteria.SortDir == SortDirection.Asc ? "ASC" : "DESC";
        return $"ORDER BY {ColumnFor(sortBy)} {direction}, report_id DESC";
    }
}
