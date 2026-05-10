using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Analytics;

namespace VoW.Api.Repositories;

public sealed class AnalyticsRepository(IConfiguration configuration) : IAnalyticsRepository
{
    private sealed record DailyUsageDbRow(
        DateTime Date,
        uint Bootups);

    public async Task<IReadOnlyList<DailyUsageRow>> GetDailyUsageAsync(
        int? days,
        CancellationToken cancellationToken)
    {
        var whereSql = days is null ? string.Empty : "WHERE date >= DATE_SUB(CURRENT_DATE(), INTERVAL @Days DAY)";
        var sql = $"""
            SELECT
                date AS Date,
                bootups AS Bootups
            FROM daily
            {whereSql}
            ORDER BY date ASC;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(sql, new { Days = days }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<DailyUsageDbRow>(command))
            .Select(row => new DailyUsageRow(DateOnly.FromDateTime(row.Date), checked((int)row.Bootups)))
            .ToList();
    }

    public async Task<int?> GetPreviousPeriodBootupsAsync(
        int days,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(SUM(bootups), 0)
            FROM daily
            WHERE date >= DATE_SUB(CURRENT_DATE(), INTERVAL @PreviousDays DAY)
              AND date < DATE_SUB(CURRENT_DATE(), INTERVAL @Days DAY);
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { Days = days, PreviousDays = days * 2 },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int?>(command);
    }
}
