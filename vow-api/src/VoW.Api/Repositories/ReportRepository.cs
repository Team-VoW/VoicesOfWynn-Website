using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Reports;

namespace VoW.Api.Repositories;

public sealed class ReportRepository(IConfiguration configuration) : IReportRepository
{
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
            ORDER BY time_submitted DESC, report_id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(command);

        command = new CommandDefinition(searchSql, parameters, cancellationToken: cancellationToken);
        var results = (await connection.QueryAsync<ReportSummary>(command)).AsList();

        return new ReportSearchPage(total, criteria.Page, results);
    }
}
