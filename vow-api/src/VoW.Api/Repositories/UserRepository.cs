using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Users;

namespace VoW.Api.Repositories;

public sealed class UserRepository(IConfiguration configuration) : IUserRepository
{
    public async Task<User?> GetAdminByDiscordIdAsync(string discordId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                CAST(discord_id AS CHAR) AS DiscordId,
                display_name AS DisplayName,
                system_admin AS SystemAdmin
            FROM user
            WHERE CAST(discord_id AS CHAR) = @DiscordId
              AND system_admin = 1
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DiscordId = discordId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(command);
    }

    public async Task<User?> GetAdminByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                CAST(discord_id AS CHAR) AS DiscordId,
                display_name AS DisplayName,
                system_admin AS SystemAdmin
            FROM user
            WHERE user_id = @UserId
              AND discord_id IS NOT NULL
              AND system_admin = 1
            LIMIT 1;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<User>(command);
    }
}
