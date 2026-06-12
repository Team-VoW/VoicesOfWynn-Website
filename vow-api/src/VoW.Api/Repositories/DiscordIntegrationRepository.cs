using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Accounts;
using VoW.Api.Domain.DiscordIntegration;

namespace VoW.Api.Repositories;

public sealed class DiscordIntegrationRepository(IConfiguration configuration) : IDiscordIntegrationRepository
{
    public async Task<IReadOnlyCollection<DiscordIntegrationUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.user_id AS UserId,
                u.display_name AS DisplayName,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.discord AS DiscordName,
                u.picture AS Picture,
                u.picture_type AS PictureType,
                dr.name AS RoleName
            FROM user u
            LEFT JOIN user_discord_role udr ON udr.user_id = u.user_id
            LEFT JOIN discord_role dr ON dr.discord_role_id = udr.discord_role_id
            WHERE u.discord_id IS NOT NULL
            ORDER BY u.user_id, dr.weight DESC, dr.name;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var rows = (await connection.QueryAsync<DiscordIntegrationUserRow>(command)).AsList();
        return rows
            .GroupBy(row => row.UserId)
            .Select(group =>
            {
                var first = group.First();
                return new DiscordIntegrationUser(
                    first.UserId,
                    first.DisplayName,
                    first.DiscordId,
                    first.DiscordName ?? string.Empty,
                    first.Picture,
                    ParsePictureType(first.PictureType),
                    group.Select(row => row.RoleName)
                        .Where(roleName => !string.IsNullOrWhiteSpace(roleName))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(roleName => roleName!)
                        .ToArray());
            })
            .ToArray();
    }

    public async Task<DiscordSyncUser?> GetUserByDiscordIdAsync(string discordId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                display_name AS DisplayName,
                CAST(discord_id AS CHAR) AS DiscordId,
                discord AS DiscordName,
                picture AS Picture,
                picture_type AS PictureType
            FROM user
            WHERE CAST(discord_id AS CHAR) = @DiscordId
            ORDER BY user_id;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DiscordId = discordId }, cancellationToken: cancellationToken);
        var user = await connection.QueryFirstOrDefaultAsync<DiscordSyncUserRow>(command);
        return user is null ? null : ToDiscordSyncUser(user);
    }

    public async Task<DiscordSyncUser?> GetUserByDiscordNameAsync(string discordName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                display_name AS DisplayName,
                CAST(discord_id AS CHAR) AS DiscordId,
                discord AS DiscordName,
                picture AS Picture,
                picture_type AS PictureType
            FROM user
            WHERE UPPER(discord) = UPPER(@DiscordName)
            ORDER BY user_id;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DiscordName = discordName }, cancellationToken: cancellationToken);
        var user = await connection.QueryFirstOrDefaultAsync<DiscordSyncUserRow>(command);
        return user is null ? null : ToDiscordSyncUser(user);
    }

    public async Task<bool> DisplayNameExistsAsync(int exceptUserId, string displayName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM user
            WHERE display_name = @DisplayName
              AND user_id <> @ExceptUserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, DisplayName = displayName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> DiscordIdExistsAsync(int exceptUserId, string discordId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM user
            WHERE CAST(discord_id AS CHAR) = @DiscordId
              AND user_id <> @ExceptUserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, DiscordId = discordId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> DiscordNameExistsAsync(int exceptUserId, string discordName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM user
            WHERE UPPER(discord) = UPPER(@DiscordName)
              AND user_id <> @ExceptUserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, DiscordName = discordName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetRoleIdsByNameAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT discord_role_id AS Id, name AS Name FROM discord_role;";

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var roles = await connection.QueryAsync<RoleRow>(command);
        return roles.ToDictionary(role => role.Name, role => role.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<int> InsertUserAsync(CreateDiscordSyncUserCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO user (display_name, password, discord_id, discord, force_password_change)
            VALUES (@DisplayName, @PasswordHash, @DiscordId, @DiscordName, 1);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var definition = new CommandDefinition(sql, command, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(definition);
    }

    public async Task<bool> UpdateDiscordFieldsAsync(
        int userId,
        string discordId,
        string discordName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE user
            SET discord_id = @DiscordId,
                discord = @DiscordName
            WHERE user_id = @UserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, DiscordId = discordId, DiscordName = discordName },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var deleteCommand = new CommandDefinition(
                "DELETE FROM user_discord_role WHERE user_id = @UserId;",
                new { UserId = userId },
                transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(deleteCommand);

            if (roleIds.Count > 0)
            {
                const string insertSql = """
                    INSERT INTO user_discord_role (user_id, discord_role_id)
                    VALUES (@UserId, @RoleId);
                    """;
                var insertCommand = new CommandDefinition(
                    insertSql,
                    roleIds.Distinct().Select(roleId => new { UserId = userId, RoleId = roleId }),
                    transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(insertCommand);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed class DiscordIntegrationUserRow
    {
        public int UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string DiscordId { get; set; } = string.Empty;

        public string? DiscordName { get; set; }

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;

        public string? RoleName { get; set; }
    }

    private sealed class DiscordSyncUserRow
    {
        public int UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? DiscordId { get; set; }

        public string? DiscordName { get; set; }

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;
    }

    private sealed class RoleRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private static DiscordSyncUser ToDiscordSyncUser(DiscordSyncUserRow user) =>
        new(
            user.UserId,
            user.DisplayName,
            user.DiscordId,
            user.DiscordName,
            user.Picture,
            ParsePictureType(user.PictureType));

    private static PictureType ParsePictureType(string value) =>
        value switch
        {
            "default" => PictureType.Default,
            "discord" => PictureType.Discord,
            "manual" => PictureType.Manual,
            _ => throw new InvalidOperationException($"Unknown picture type '{value}'.")
        };

}
