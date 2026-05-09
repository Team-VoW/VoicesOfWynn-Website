using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Users;

namespace VoW.Api.Repositories;

public sealed class UserRepository(IConfiguration configuration) : IUserRepository
{
    public async Task<UserProfile?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                CAST(discord_id AS CHAR) AS DiscordId,
                display_name AS DisplayName
            FROM user
            WHERE CAST(discord_id AS CHAR) = @DiscordId
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DiscordId = discordId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);

        return row is null ? null : await BuildUserProfileAsync(connection, row, cancellationToken);
    }

    public async Task<UserProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                CAST(discord_id AS CHAR) AS DiscordId,
                display_name AS DisplayName
            FROM user
            WHERE user_id = @UserId
              AND discord_id IS NOT NULL
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);

        return row is null ? null : await BuildUserProfileAsync(connection, row, cancellationToken);
    }

    private static async Task<UserProfile> BuildUserProfileAsync(
        MySqlConnection connection,
        UserRow row,
        CancellationToken cancellationToken)
    {
        const string roleSql = """
            SELECT discord_role_id
            FROM user_discord_role
            WHERE user_id = @UserId;
            """;

        var command = new CommandDefinition(roleSql, new { row.UserId }, cancellationToken: cancellationToken);
        var roleIds = await connection.QueryAsync<int>(command);
        var roles = roleIds
            .Where(roleId => Enum.IsDefined(typeof(DiscordRoleId), roleId))
            .Select(roleId => (DiscordRoleId)roleId)
            .Distinct()
            .ToArray();

        return new UserProfile(row.UserId, row.DiscordId, row.DisplayName, roles);
    }

    private sealed record UserRow(
        int UserId,
        string DiscordId,
        string DisplayName);
}
