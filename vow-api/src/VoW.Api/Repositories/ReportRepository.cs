using Dapper;
using MySqlConnector;
using VoW.Api.Models;

namespace VoW.Api.Repositories;

public sealed class ReportRepository(IConfiguration configuration) : IReportRepository
{
    public async Task<ReportSearchResponse> SearchAsync(ReportSearchRequest request, CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Npc))
        {
            where.Add("npc_name LIKE @Npc");
            parameters.Add("Npc", $"%{request.Npc}%");
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            where.Add("chat_message LIKE @Content");
            parameters.Add("Content", $"%{request.Content}%");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            where.Add("status = @Status");
            parameters.Add("Status", request.Status.ToLowerInvariant());
        }

        var whereSql = where.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", where)}";
        var offset = (request.Page - 1) * request.PageSize;
        parameters.Add("PageSize", request.PageSize);
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
        var results = (await connection.QueryAsync<ReportSearchResult>(command)).AsList();

        return new ReportSearchResponse(total, request.Page, results);
    }
}
