using Dapper;
using MySqlConnector;

namespace VoW.Api.Repositories;

/// <summary>
/// Counts writes into hourly buckets keyed by an opaque hash. The backing table is named for
/// quest feedback, which introduced it, but nothing about it is feedback specific - callers
/// separate their budgets by hashing a prefix into the key.
/// </summary>
public sealed class WriteLimitRepository(IConfiguration configuration) : IWriteLimitRepository
{
    public async Task<bool> ConsumeLimitAsync(byte[] key, int limit, CancellationToken ct)
    {
        await using var db = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
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
            "SELECT writes FROM quest_feedback_write_limit WHERE bucket_key=@key AND window_start=@window",
            args, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM quest_feedback_write_limit WHERE window_start < @old LIMIT 1000",
            new { old = window.AddHours(-2) }, cancellationToken: ct));
        return count <= limit;
    }
}
