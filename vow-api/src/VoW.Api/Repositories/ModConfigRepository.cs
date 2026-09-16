using System.Text.Json;
using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Mod;

namespace VoW.Api.Repositories;

public sealed class ModConfigRepository(IConfiguration configuration) : IModConfigRepository
{
    private sealed record ReleaseDbRow(
        string LatestVersion,
        string UpdateNotificationVersion,
        string KillSwitchVersion,
        string DownloadUrl,
        string ChangelogUrl,
        string AudioBaseUrl,
        string AudioMirrorUrls,
        DateTime UpdatedAt,
        int? UpdatedBy);

    public async Task<ModRelease?> GetReleaseAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                latest_version AS LatestVersion,
                update_notification_version AS UpdateNotificationVersion,
                kill_switch_version AS KillSwitchVersion,
                download_url AS DownloadUrl,
                changelog_url AS ChangelogUrl,
                audio_base_url AS AudioBaseUrl,
                audio_mirror_urls AS AudioMirrorUrls,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy
            FROM mod_release
            WHERE id = 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var row = await connection.QuerySingleOrDefaultAsync<ReleaseDbRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new ModRelease(
                row.LatestVersion,
                row.UpdateNotificationVersion,
                row.KillSwitchVersion,
                row.DownloadUrl,
                row.ChangelogUrl,
                row.AudioBaseUrl,
                DeserializeMirrors(row.AudioMirrorUrls),
                AsUtc(row.UpdatedAt),
                row.UpdatedBy);
    }

    public async Task UpdateReleaseAsync(ModRelease release, int? updatedBy, CancellationToken cancellationToken)
    {
        // Upsert rather than update: the seed row is expected, but a fresh database that somehow
        // skipped it would otherwise silently accept a write that changed nothing.
        const string sql = """
            INSERT INTO mod_release (
                id, latest_version, update_notification_version, kill_switch_version,
                download_url, changelog_url, audio_base_url, audio_mirror_urls, updated_by)
            VALUES (
                1, @LatestVersion, @UpdateNotificationVersion, @KillSwitchVersion,
                @DownloadUrl, @ChangelogUrl, @AudioBaseUrl, @AudioMirrorUrls, @UpdatedBy)
            ON DUPLICATE KEY UPDATE
                latest_version = VALUES(latest_version),
                update_notification_version = VALUES(update_notification_version),
                kill_switch_version = VALUES(kill_switch_version),
                download_url = VALUES(download_url),
                changelog_url = VALUES(changelog_url),
                audio_base_url = VALUES(audio_base_url),
                audio_mirror_urls = VALUES(audio_mirror_urls),
                updated_by = VALUES(updated_by);
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                release.LatestVersion,
                release.UpdateNotificationVersion,
                release.KillSwitchVersion,
                release.DownloadUrl,
                release.ChangelogUrl,
                release.AudioBaseUrl,
                AudioMirrorUrls = JsonSerializer.Serialize(release.AudioMirrorUrls),
                UpdatedBy = updatedBy
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ModBroadcast>> GetBroadcastsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                broadcast_id AS Id,
                content AS Content,
                active_from AS ActiveFrom,
                active_until AS ActiveUntil,
                created_at AS CreatedAt,
                created_by AS CreatedBy
            FROM mod_broadcast
            ORDER BY active_from DESC, broadcast_id DESC;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return (await connection.QueryAsync<ModBroadcast>(
                new CommandDefinition(sql, cancellationToken: cancellationToken)))
            .Select(broadcast => broadcast with
            {
                ActiveFrom = AsUtc(broadcast.ActiveFrom),
                ActiveUntil = AsUtc(broadcast.ActiveUntil),
                CreatedAt = AsUtc(broadcast.CreatedAt)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetActiveBroadcastContentsAsync(
        DateTime at,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT content
            FROM mod_broadcast
            WHERE active_from <= @At AND active_until >= @At
            ORDER BY active_from ASC, broadcast_id ASC;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return (await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { At = at }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<int> CreateBroadcastAsync(ModBroadcast broadcast, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO mod_broadcast (content, active_from, active_until, created_by)
            VALUES (@Content, @ActiveFrom, @ActiveUntil, @CreatedBy);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { broadcast.Content, broadcast.ActiveFrom, broadcast.ActiveUntil, broadcast.CreatedBy },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateBroadcastAsync(ModBroadcast broadcast, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE mod_broadcast
            SET content = @Content, active_from = @ActiveFrom, active_until = @ActiveUntil
            WHERE broadcast_id = @Id;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        // Rows matched, not rows changed: saving a broadcast without editing it is still a hit.
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { broadcast.Id, broadcast.Content, broadcast.ActiveFrom, broadcast.ActiveUntil },
            cancellationToken: cancellationToken)) > 0
            || await ExistsAsync("mod_broadcast", "broadcast_id", broadcast.Id, cancellationToken);
    }

    public async Task<bool> DeleteBroadcastAsync(int broadcastId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM mod_broadcast WHERE broadcast_id = @Id;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = broadcastId }, cancellationToken: cancellationToken)) > 0;
    }

    public async Task<IReadOnlyList<FunFact>> GetFunFactsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fun_fact_id AS Id,
                slug AS Slug,
                content AS Content,
                active AS Active,
                created_at AS CreatedAt
            FROM fun_fact
            ORDER BY slug ASC;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return (await connection.QueryAsync<FunFact>(
                new CommandDefinition(sql, cancellationToken: cancellationToken)))
            .Select(funFact => funFact with { CreatedAt = AsUtc(funFact.CreatedAt) })
            .ToList();
    }

    public async Task<string?> GetRandomActiveFunFactAsync(CancellationToken cancellationToken)
    {
        // The table is a few dozen rows, so ORDER BY RAND() costs nothing here and keeps the
        // selection in one round trip. The PHP version globbed the filesystem on every mod boot.
        const string sql = "SELECT content FROM fun_fact WHERE active = 1 ORDER BY RAND() LIMIT 1;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveFunFactsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM fun_fact WHERE active = 1;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    /// <returns>The new id, or null when the slug is already taken.</returns>
    public async Task<int?> CreateFunFactAsync(FunFact funFact, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT IGNORE INTO fun_fact (slug, content, active)
            VALUES (@Slug, @Content, @Active);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { funFact.Slug, funFact.Content, funFact.Active },
            cancellationToken: cancellationToken));

        // INSERT IGNORE leaves LAST_INSERT_ID() at 0 when the unique slug index rejected the row.
        return id == 0 ? null : id;
    }

    public async Task<bool> UpdateFunFactAsync(FunFact funFact, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE fun_fact
            SET slug = @Slug, content = @Content, active = @Active
            WHERE fun_fact_id = @Id;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { funFact.Id, funFact.Slug, funFact.Content, funFact.Active },
            cancellationToken: cancellationToken)) > 0
            || await ExistsAsync("fun_fact", "fun_fact_id", funFact.Id, cancellationToken);
    }

    public async Task<bool> DeleteFunFactAsync(int funFactId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM fun_fact WHERE fun_fact_id = @Id;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = funFactId }, cancellationToken: cancellationToken)) > 0;
    }

    private async Task<bool> ExistsAsync(string table, string idColumn, int id, CancellationToken cancellationToken)
    {
        // table/idColumn are compile-time literals from this class only - never request data.
        var sql = $"SELECT 1 FROM {table} WHERE {idColumn} = @Id LIMIT 1;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetApiConnectionString(configuration));
        return await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)) is not null;
    }

    /// <summary>
    /// MySQL DATETIME carries no offset, so a value read back is Unspecified. Everything here is
    /// written as UTC; saying so keeps the serialized JSON unambiguous for the browser.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static IReadOnlyList<string> DeserializeMirrors(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
