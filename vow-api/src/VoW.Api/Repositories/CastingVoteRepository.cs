using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Casting;

namespace VoW.Api.Repositories;

public sealed class CastingVoteRepository(IConfiguration configuration) : ICastingVoteRepository
{
    public async Task<IReadOnlyList<CastingVote>> GetVotesForRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.audition_id AS AuditionId, a.character_id AS CharacterId, v.user_id AS UserId, v.picked AS Picked, v.comment AS Comment
            FROM casting_vote v
            JOIN casting_audition a ON a.audition_id = v.audition_id
            JOIN casting_character c ON c.character_id = a.character_id
            WHERE c.round_id = @RoundId
            ORDER BY v.created_at, v.audition_id;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingVote>(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CastingVote>> GetVotesForCharacterAsync(
        int characterId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.audition_id AS AuditionId, a.character_id AS CharacterId, v.user_id AS UserId, v.picked AS Picked, v.comment AS Comment
            FROM casting_vote v
            JOIN casting_audition a ON a.audition_id = v.audition_id
            WHERE a.character_id = @CharacterId
            ORDER BY v.created_at, v.audition_id;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingVote>(new CommandDefinition(
            sql, new { CharacterId = characterId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task UpsertVoteAsync(int auditionId, int userId, string? comment, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO casting_vote (audition_id, user_id, picked, comment)
            VALUES (@AuditionId, @UserId, 1, @Comment)
            ON DUPLICATE KEY UPDATE picked = 1, comment = VALUES(comment);
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { AuditionId = auditionId, UserId = userId, Comment = comment }, cancellationToken: cancellationToken));
    }

    public async Task UpsertCommentAsync(int auditionId, int userId, string comment, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO casting_vote (audition_id, user_id, picked, comment)
            VALUES (@AuditionId, @UserId, 0, @Comment)
            ON DUPLICATE KEY UPDATE comment = VALUES(comment);
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { AuditionId = auditionId, UserId = userId, Comment = comment }, cancellationToken: cancellationToken));
    }

    public async Task UnpickAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_vote SET picked = 0 WHERE audition_id = @AuditionId AND user_id = @UserId;
            DELETE FROM casting_vote
            WHERE audition_id = @AuditionId AND user_id = @UserId AND picked = 0 AND comment IS NULL;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { AuditionId = auditionId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UnpickAllForCharacterAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_vote v
            JOIN casting_audition a ON a.audition_id = v.audition_id
            SET v.picked = 0
            WHERE a.character_id = @CharacterId AND v.user_id = @UserId;
            DELETE v FROM casting_vote v
            JOIN casting_audition a ON a.audition_id = v.audition_id
            WHERE a.character_id = @CharacterId AND v.user_id = @UserId AND v.picked = 0 AND v.comment IS NULL;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { CharacterId = characterId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteCommentAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_vote SET comment = NULL WHERE audition_id = @AuditionId AND user_id = @UserId;
            DELETE FROM casting_vote
            WHERE audition_id = @AuditionId AND user_id = @UserId AND picked = 0;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { AuditionId = auditionId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CastingDone>> GetDoneForRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d.character_id AS CharacterId, d.user_id AS UserId
            FROM casting_character_done d
            JOIN casting_character c ON c.character_id = d.character_id
            WHERE c.round_id = @RoundId;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingDone>(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task SetDoneAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        const string sql = "INSERT IGNORE INTO casting_character_done (character_id, user_id) VALUES (@CharacterId, @UserId);";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { CharacterId = characterId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task ClearDoneAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM casting_character_done WHERE character_id = @CharacterId AND user_id = @UserId;";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { CharacterId = characterId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CastingVoter>> GetUsersWithRolesAsync(
        IReadOnlyCollection<DiscordRoleId> roles,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT u.user_id AS UserId, u.display_name AS DisplayName
            FROM user u
            JOIN user_discord_role udr ON udr.user_id = u.user_id
            WHERE udr.discord_role_id IN @RoleIds
            ORDER BY u.display_name;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingVoter>(new CommandDefinition(
            sql, new { RoleIds = roles.Select(role => (int)role).ToArray() }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CastingVoter>> GetUsersAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        const string sql = "SELECT user_id AS UserId, display_name AS DisplayName FROM user WHERE user_id IN @UserIds;";

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingVoter>(new CommandDefinition(
            sql, new { UserIds = userIds.ToArray() }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private MySqlConnection Open() => new(DatabaseSettings.GetWebsiteConnectionString(configuration));
}
