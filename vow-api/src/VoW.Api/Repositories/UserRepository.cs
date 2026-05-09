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
                u.user_id AS UserId,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.display_name AS DisplayName,
                udr.discord_role_id AS RoleId
            FROM user u
            LEFT JOIN user_discord_role udr ON udr.user_id = u.user_id
            WHERE CAST(u.discord_id AS CHAR) = @DiscordId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DiscordId = discordId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<UserRoleRow>(command);

        return BuildUserProfile(rows);
    }

    public async Task<UserProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.user_id AS UserId,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.display_name AS DisplayName,
                udr.discord_role_id AS RoleId
            FROM user u
            LEFT JOIN user_discord_role udr ON udr.user_id = u.user_id
            WHERE u.user_id = @UserId
              AND u.discord_id IS NOT NULL;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<UserRoleRow>(command);

        return BuildUserProfile(rows);
    }

    private static UserProfile? BuildUserProfile(IEnumerable<UserRoleRow> rows)
    {
        var userRows = rows.ToArray();
        var first = userRows.FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        var roles = userRows
            .Select(row => row.RoleId)
            .Where(roleId => roleId.HasValue && Enum.IsDefined(typeof(DiscordRoleId), roleId.Value))
            .Select(roleId => (DiscordRoleId)roleId!.Value)
            .Distinct()
            .ToArray();

        return new UserProfile(first.UserId, first.DiscordId, first.DisplayName, roles);
    }

    private sealed record UserRoleRow(
        int UserId,
        string DiscordId,
        string DisplayName,
        int? RoleId);
}
