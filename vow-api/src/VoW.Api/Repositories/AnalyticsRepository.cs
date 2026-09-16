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
        var whereSql = days is null ? string.Empty : "WHERE date >= DATE_SUB(CURRENT_DATE(), INTERVAL (@Days - 1) DAY)";
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

    public async Task<bool> RecordBootupAsync(
        string clientIdHash,
        string ipHash,
        CancellationToken cancellationToken)
    {
        // One round trip for both throttles, served by the uuid_time / ip_time indexes.
        const string throttleSql = """
            SELECT EXISTS(
                SELECT 1 FROM ping
                WHERE (uuid = @ClientId AND time > @ClientCutoff)
                   OR (ip = @Ip AND time > @IpCutoff)
                LIMIT 1);
            """;
        const string insertPingSql = "INSERT INTO ping (uuid, ip, time) VALUES (@ClientId, @Ip, @Now);";

        // The PHP version tried to detect the duplicate-key error by comparing PDO's getCode()
        // against MySQL's 1062, but PDO returns the SQLSTATE '23000' - so every returning player
        // took the failure branch and the whole bootup response came back as a 500. INSERT IGNORE
        // removes the need to inspect the error at all.
        const string insertTotalSql = "INSERT IGNORE INTO total (uuid) VALUES (@ClientId);";

        var now = DateTime.UtcNow;
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var throttled = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            throttleSql,
            new
            {
                ClientId = clientIdHash,
                Ip = ipHash,
                ClientCutoff = now - BootupThrottle.ByClientId,
                IpCutoff = now - BootupThrottle.ByIp
            },
            cancellationToken: cancellationToken));

        if (throttled)
        {
            return false;
        }

        var args = new { ClientId = clientIdHash, Ip = ipHash, Now = now };
        await connection.ExecuteAsync(new CommandDefinition(insertPingSql, args, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(insertTotalSql, args, cancellationToken: cancellationToken));
        return true;
    }

    public async Task<UsageAggregationResult> AggregateAsync(
        DateOnly throughDate,
        int maxDays,
        CancellationToken cancellationToken)
    {
        // Resume after the newest aggregated day; on a database that has never been aggregated,
        // start at the oldest ping. The PHP version read MAX(date) unguarded, so an empty daily
        // table produced new DateTime(null) - "now" - and the table never seeded at all.
        const string startSql = """
            SELECT COALESCE(
                (SELECT DATE_ADD(MAX(date), INTERVAL 1 DAY) FROM daily),
                (SELECT MIN(DATE(time)) FROM ping));
            """;

        // Half-open range instead of the old `time LIKE 'Y-m-d%'`, which could not use an index.
        // The unique key on daily.date makes a second run a no-op rather than a duplicate row.
        const string rollUpSql = """
            INSERT INTO daily (date, bootups)
            SELECT @Day, COUNT(*) FROM ping WHERE time >= @Day AND time < @NextDay
            ON DUPLICATE KEY UPDATE bootups = bootups + VALUES(bootups);
            """;
        const string deleteSql = "DELETE FROM ping WHERE time >= @Day AND time < @NextDay;";

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);

        var start = await connection.ExecuteScalarAsync<DateTime?>(
            new CommandDefinition(startSql, cancellationToken: cancellationToken));
        if (start is null)
        {
            return new UsageAggregationResult(0, 0, null);
        }

        var day = DateOnly.FromDateTime(start.Value);
        var daysProcessed = 0;
        var bootupsAggregated = 0;
        DateOnly? lastProcessed = null;

        while (day < throughDate && daysProcessed < maxDays)
        {
            var args = new { Day = day.ToDateTime(TimeOnly.MinValue), NextDay = day.AddDays(1).ToDateTime(TimeOnly.MinValue) };

            // Count and delete in one transaction. The PHP version counted first and opened the
            // transaction afterwards, so pings written in between were deleted but never counted.
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            bootupsAggregated += await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM ping WHERE time >= @Day AND time < @NextDay;",
                args, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(rollUpSql, args, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(deleteSql, args, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);

            lastProcessed = day;
            daysProcessed++;
            day = day.AddDays(1);
        }

        return new UsageAggregationResult(daysProcessed, bootupsAggregated, lastProcessed);
    }
}
