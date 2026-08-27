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

    public async Task<VoicedLineImportCounts> MarkLinesAsVoicedAsync(
        IReadOnlyList<string> chatMessages,
        int chunkSize,
        CancellationToken cancellationToken)
    {
        // Dapper expands the @Lines list into @Lines1..@LinesN and adds the parentheses itself,
        // so the SQL must not write them. Chunks are a fixed size because Dapper caches the
        // expanded SQL per parameter count.
        const string countSql = "SELECT COUNT(*) FROM report WHERE chat_message IN @Lines;";
        const string updateSql = "UPDATE report SET status = 'fixed' WHERE chat_message IN @Lines;";
        const string insertSql = """
            INSERT IGNORE INTO report
                (chat_message, npc_name, player, pos_x, pos_y, pos_z, reported_times, status)
            VALUES
                (@ChatMessage, NULL, '<IMPORT>', NULL, NULL, NULL, 0, 'fixed');
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var alreadyPresent = 0;
        var inserted = 0;

        for (var offset = 0; offset < chatMessages.Count; offset += chunkSize)
        {
            var chunk = Chunk(chatMessages, offset, chunkSize);

            // Counted before the UPDATE: an UPDATE's affected-rows reports only rows whose value
            // actually changed, so lines already at 'fixed' would report 0 on a re-import.
            alreadyPresent += await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(countSql, new { Lines = chunk }, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new { Lines = chunk }, cancellationToken: cancellationToken));

            // INSERT IGNORE's affected-rows is unambiguous: ignored duplicates count 0.
            inserted += await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    chunk.Select(line => new { ChatMessage = line }).ToArray(),
                    cancellationToken: cancellationToken));
        }

        // Final sweep. The mod client can insert a report row for one of these lines in the window
        // between a chunk's UPDATE and its INSERT IGNORE - the UPDATE missed it (it did not exist
        // yet) and the INSERT IGNORE dropped it (the unique index now hits), leaving it
        // 'unprocessed'. MyISAM has no transactions to close that window, so re-run the UPDATE.
        for (var offset = 0; offset < chatMessages.Count; offset += chunkSize)
        {
            var chunk = Chunk(chatMessages, offset, chunkSize);
            await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new { Lines = chunk }, cancellationToken: cancellationToken));
        }

        return new VoicedLineImportCounts(alreadyPresent, inserted);
    }

    public async Task<bool> DeleteAsync(int reportId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM report WHERE report_id = @Id;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(sql, new { Id = reportId }, cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

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
