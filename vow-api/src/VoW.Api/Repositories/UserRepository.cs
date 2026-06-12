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
            WHERE u.user_id = @UserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<UserRoleRow>(command);

        return BuildUserProfile(rows);
    }

    public async Task<PasswordLoginUser?> GetForPasswordLoginAsync(string username, CancellationToken cancellationToken)
    {
        const string resolveUserSql = """
            SELECT
                u.user_id AS UserId,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.display_name AS DisplayName,
                u.email AS Email,
                u.password AS PasswordHash,
                u.force_password_change AS ForcePasswordChange
            FROM user u
            WHERE u.email = @Username
               OR u.display_name = @Username
            ORDER BY CASE WHEN u.email = @Username THEN 0 ELSE 1 END,
                     u.user_id
            LIMIT 2;
            """;

        const string rolesSql = """
            SELECT
                u.user_id AS UserId,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.display_name AS DisplayName,
                udr.discord_role_id AS RoleId
            FROM user u
            LEFT JOIN user_discord_role udr ON udr.user_id = u.user_id
            WHERE u.user_id = @UserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(resolveUserSql, new { Username = username }, cancellationToken: cancellationToken);
        var candidates = (await connection.QueryAsync<PasswordLoginCandidateRow>(command)).AsList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var exactEmailMatches = candidates.Where(row => string.Equals(row.Email, username, StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected = exactEmailMatches.Length switch
        {
            1 => exactEmailMatches[0],
            > 1 => null,
            _ => candidates.Count == 1 ? candidates[0] : null
        };
        if (selected is null)
        {
            return null;
        }

        command = new CommandDefinition(rolesSql, new { selected.UserId }, cancellationToken: cancellationToken);
        var roleRows = await connection.QueryAsync<UserRoleRow>(command);
        var profile = BuildUserProfile(roleRows);

        return profile is null
            ? null
            : new PasswordLoginUser(selected.UserId, selected.PasswordHash, selected.ForcePasswordChange, profile);
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
        string? DiscordId,
        string DisplayName,
        int? RoleId);

    private sealed record PasswordLoginCandidateRow(
        int UserId,
        string? DiscordId,
        string DisplayName,
        string? Email,
        string? PasswordHash,
        bool ForcePasswordChange);
}
