using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Content;

namespace VoW.Api.Repositories;

public sealed class ContentRepository(IConfiguration configuration) : IContentRepository
{
    public async Task<IReadOnlyCollection<ContentOption>> GetQuestsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT quest_id AS Id, name AS Name
            FROM quest
            ORDER BY name;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ContentOption>(command)).AsList();
    }

    public async Task<IReadOnlyCollection<ContentOption>> GetUsersByRolesAsync(
        IReadOnlyCollection<DiscordRoleId> roles,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT u.user_id AS Id, u.display_name AS Name
            FROM user u
            JOIN user_discord_role udr ON udr.user_id = u.user_id
            WHERE udr.discord_role_id IN @RoleIds
            ORDER BY u.display_name;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { RoleIds = roles.Select(role => (int)role).ToArray() },
            cancellationToken: cancellationToken);
        return (await connection.QueryAsync<ContentOption>(command)).AsList();
    }

    public async Task<bool> QuestDegeneratedNameExistsAsync(string degeneratedName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM quest WHERE degenerated_name = @DegeneratedName;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<CreatedContent> CreateQuestAsync(
        CreateQuestCommand command,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO quest (name, degenerated_name, writer)
            VALUES (@Name, @DegeneratedName, @WriterUserId);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var definition = new CommandDefinition(
            sql,
            new { command.Name, DegeneratedName = degeneratedName, command.WriterUserId },
            cancellationToken: cancellationToken);
        var id = await connection.ExecuteScalarAsync<int>(definition);
        return new CreatedContent(id);
    }

    public async Task<CreatedContent> CreateNpcAsync(
        CreateNpcCommand command,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string npcSql = """
                INSERT INTO npc (name, degenerated_name, voice_actor_id)
                VALUES (@Name, @DegeneratedName, @VoiceActorUserId);
                SELECT LAST_INSERT_ID();
                """;

            var npcCommand = new CommandDefinition(
                npcSql,
                new { command.Name, DegeneratedName = degeneratedName, command.VoiceActorUserId },
                transaction,
                cancellationToken: cancellationToken);
            var npcId = await connection.ExecuteScalarAsync<int>(npcCommand);

            const string questSql = """
                INSERT INTO npc_quest (quest_id, npc_id, sorting_order, editor)
                VALUES (
                    @QuestId,
                    @NpcId,
                    (SELECT COALESCE(MAX(so), 0) + 1
                     FROM (SELECT sorting_order AS so FROM npc_quest WHERE quest_id = @QuestId) AS sub),
                    @SoundEditorUserId
                );
                """;

            foreach (var assignment in command.QuestAssignments)
            {
                var questCommand = new CommandDefinition(
                    questSql,
                    new { assignment.QuestId, NpcId = npcId, assignment.SoundEditorUserId },
                    transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(questCommand);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CreatedContent(npcId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
