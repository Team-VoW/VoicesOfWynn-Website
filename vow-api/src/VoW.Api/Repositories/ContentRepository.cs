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

    public async Task<IReadOnlyCollection<ContentOption>> GetNpcsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT npc_id AS Id, name AS Name
            FROM npc
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

    public async Task<bool> QuestDegeneratedNameExistsAsync(
        int exceptQuestId,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM quest
            WHERE quest_id <> @ExceptQuestId AND degenerated_name = @DegeneratedName;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptQuestId = exceptQuestId, DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> QuestExistsAsync(int questId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM quest WHERE quest_id = @QuestId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM npc WHERE npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<string?> GetQuestDegeneratedNameAsync(int questId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT degenerated_name FROM quest WHERE quest_id = @QuestId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(command);
    }

    public async Task<string?> GetNpcDegeneratedNameAsync(int npcId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT degenerated_name FROM npc WHERE npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(command);
    }

    public async Task<bool> NpcDegeneratedNameConflictsForLinkedQuestsAsync(
        int npcId,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM npc_quest target
            JOIN npc_quest other_link ON other_link.quest_id = target.quest_id
            JOIN npc other_npc ON other_npc.npc_id = other_link.npc_id
            WHERE target.npc_id = @NpcId
              AND other_link.npc_id <> @NpcId
              AND other_npc.degenerated_name = @DegeneratedName;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { NpcId = npcId, DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> NpcDegeneratedNameConflictsInQuestAsync(
        int questId,
        int npcId,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM npc_quest nq
            JOIN npc n ON n.npc_id = nq.npc_id
            WHERE nq.quest_id = @QuestId
              AND nq.npc_id <> @NpcId
              AND n.degenerated_name = @DegeneratedName;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId, DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> QuestHasNpcsAsync(int questId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM npc_quest WHERE quest_id = @QuestId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> QuestNpcLinkExistsAsync(int questId, int npcId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM npc_quest WHERE quest_id = @QuestId AND npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> QuestNpcHasRecordingsAsync(int questId, int npcId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM recording WHERE quest_id = @QuestId AND npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<IReadOnlyCollection<NpcRecording>> GetQuestNpcRecordingsAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT recording_id AS RecordingId, line AS Line, file AS FileName
            FROM recording
            WHERE quest_id = @QuestId AND npc_id = @NpcId
            ORDER BY line, recording_id;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<NpcRecording>(command)).AsList();
    }

    public async Task<RecordingFile?> GetQuestNpcRecordingFileAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT recording_id AS RecordingId, file AS File
            FROM recording
            WHERE quest_id = @QuestId AND npc_id = @NpcId AND recording_id = @RecordingId
            LIMIT 1;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { QuestId = questId, NpcId = npcId, RecordingId = recordingId },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RecordingFile>(command);
    }

    public async Task<RecordingFile?> GetRecordingByFileAsync(string fileName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT recording_id AS RecordingId, file AS File
            FROM recording
            WHERE file = @FileName
            ORDER BY recording_id
            LIMIT 1;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { FileName = fileName }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<RecordingFile>(command);
    }

    public async Task<bool> UpdateRecordingFileAsync(
        int recordingId,
        string fileName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE recording
            SET file = @FileName
            WHERE recording_id = @RecordingId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { RecordingId = recordingId, FileName = fileName },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<CreatedContent> InsertRecordingAsync(
        int questId,
        int npcId,
        int line,
        string fileName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO recording (npc_id, quest_id, line, file)
            VALUES (@NpcId, @QuestId, @Line, @FileName);
            SELECT LAST_INSERT_ID();
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { QuestId = questId, NpcId = npcId, Line = line, FileName = fileName },
            cancellationToken: cancellationToken);
        var id = await connection.ExecuteScalarAsync<int>(command);
        return new CreatedContent(id);
    }

    public async Task<bool> DeleteQuestNpcRecordingAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM recording
            WHERE quest_id = @QuestId AND npc_id = @NpcId AND recording_id = @RecordingId
            LIMIT 1;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { QuestId = questId, NpcId = npcId, RecordingId = recordingId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<ContentSearchPage> SearchAsync(ContentSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (criteria.Quest is not null)
        {
            where.Add("(q.name LIKE @Quest OR q.degenerated_name LIKE @Quest)");
            parameters.Add("Quest", $"%{criteria.Quest}%");
        }

        if (criteria.Npc is not null)
        {
            where.Add("""
                EXISTS (
                    SELECT 1
                    FROM npc_quest search_nq
                    JOIN npc search_n ON search_n.npc_id = search_nq.npc_id
                    WHERE search_nq.quest_id = q.quest_id
                      AND (search_n.name LIKE @Npc OR search_n.degenerated_name LIKE @Npc)
                )
                """);
            parameters.Add("Npc", $"%{criteria.Npc}%");
        }

        var whereSql = where.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", where)}";
        var offset = (criteria.Page - 1) * criteria.PageSize;
        parameters.Add("PageSize", criteria.PageSize);
        parameters.Add("Offset", offset);

        var countSql = $"SELECT COUNT(*) FROM quest q {whereSql};";
        var questSql = $"""
            SELECT
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName,
                q.writer AS WriterId,
                writer.display_name AS WriterName
            FROM quest q
            LEFT JOIN user writer ON writer.user_id = q.writer
            {whereSql}
            ORDER BY q.name, q.quest_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(command);

        command = new CommandDefinition(questSql, parameters, cancellationToken: cancellationToken);
        var quests = (await connection.QueryAsync<QuestContentRow>(command)).AsList();
        if (quests.Count == 0)
        {
            return new ContentSearchPage(total, criteria.Page, criteria.PageSize, []);
        }

        const string npcSql = """
            SELECT
                nq.quest_id AS QuestId,
                n.npc_id AS NpcId,
                n.name AS NpcName,
                n.degenerated_name AS NpcDegeneratedName,
                n.voice_actor_id AS VoiceActorId,
                u.display_name AS VoiceActorName,
                nq.editor AS SoundEditorId,
                editor.display_name AS SoundEditorName,
                COUNT(r.recording_id) AS RecordingCount
            FROM npc_quest nq
            JOIN npc n ON n.npc_id = nq.npc_id
            LEFT JOIN user u ON u.user_id = n.voice_actor_id
            LEFT JOIN user editor ON editor.user_id = nq.editor
            LEFT JOIN recording r ON r.quest_id = nq.quest_id AND r.npc_id = n.npc_id
            WHERE nq.quest_id IN @QuestIds
            GROUP BY nq.quest_id, n.npc_id, n.name, n.degenerated_name, n.voice_actor_id, u.display_name, nq.editor, editor.display_name, nq.sorting_order
            ORDER BY nq.quest_id, nq.sorting_order, n.name;
            """;

        command = new CommandDefinition(
            npcSql,
            new { QuestIds = quests.Select(quest => quest.QuestId).ToArray() },
            cancellationToken: cancellationToken);
        var npcs = (await connection.QueryAsync<NpcContentRow>(command)).AsList();
        var npcsByQuest = npcs.GroupBy(npc => npc.QuestId).ToDictionary(group => group.Key, group => group.ToArray());

        var results = quests.Select(quest => new QuestContentSummary(
            quest.QuestId,
            quest.QuestName,
            quest.QuestDegeneratedName,
            quest.WriterId,
            quest.WriterName,
            npcsByQuest.TryGetValue(quest.QuestId, out var questNpcs)
                ? questNpcs.Select(npc => new NpcContentSummary(
                    npc.NpcId,
                    npc.NpcName,
                    npc.NpcDegeneratedName,
                    npc.VoiceActorId,
                    npc.VoiceActorName,
                    npc.SoundEditorId,
                    npc.SoundEditorName,
                    npc.RecordingCount)).ToArray()
                : [])).ToArray();

        return new ContentSearchPage(total, criteria.Page, criteria.PageSize, results);
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

            const string nextOrderSql = """
                SELECT COALESCE(MAX(sorting_order), 0) + 1
                FROM npc_quest
                WHERE quest_id = @QuestId
                FOR UPDATE;
                """;

            const string insertQuestSql = """
                INSERT INTO npc_quest (quest_id, npc_id, sorting_order, editor)
                VALUES (@QuestId, @NpcId, @SortingOrder, @SoundEditorUserId);
                """;

            foreach (var assignment in command.QuestAssignments)
            {
                var nextOrderCommand = new CommandDefinition(
                    nextOrderSql,
                    new { assignment.QuestId },
                    transaction,
                    cancellationToken: cancellationToken);
                var sortingOrder = await connection.ExecuteScalarAsync<int>(nextOrderCommand);

                var insertCommand = new CommandDefinition(
                    insertQuestSql,
                    new { assignment.QuestId, NpcId = npcId, SortingOrder = sortingOrder, assignment.SoundEditorUserId },
                    transaction,
                    cancellationToken: cancellationToken);
                await connection.ExecuteAsync(insertCommand);
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

    public async Task<bool> UpdateQuestAsync(
        int questId,
        string name,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE quest
            SET name = @Name, degenerated_name = @DegeneratedName
            WHERE quest_id = @QuestId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, Name = name, DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> UpdateQuestWriterAsync(
        int questId,
        int? writerUserId,
        CancellationToken cancellationToken)
    {
        const string sql = "UPDATE quest SET writer = @WriterUserId WHERE quest_id = @QuestId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, WriterUserId = writerUserId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> DeleteQuestAsync(int questId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM quest WHERE quest_id = @QuestId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> UpdateNpcAsync(
        int npcId,
        string name,
        string degeneratedName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE npc
            SET name = @Name, degenerated_name = @DegeneratedName
            WHERE npc_id = @NpcId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { NpcId = npcId, Name = name, DegeneratedName = degeneratedName }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> UpdateNpcVoiceActorAsync(int npcId, int? voiceActorUserId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE npc SET voice_actor_id = @VoiceActorUserId WHERE npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { NpcId = npcId, VoiceActorUserId = voiceActorUserId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> LinkNpcToQuestAsync(int questId, int npcId, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO npc_quest (quest_id, npc_id, sorting_order)
            VALUES (
                @QuestId,
                @NpcId,
                (SELECT COALESCE(MAX(so), 0) + 1
                 FROM (SELECT sorting_order AS so FROM npc_quest WHERE quest_id = @QuestId) AS sub)
            );
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> UpdateQuestNpcSoundEditorAsync(
        int questId,
        int npcId,
        int? soundEditorUserId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE npc_quest
            SET editor = @SoundEditorUserId
            WHERE quest_id = @QuestId AND npc_id = @NpcId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { QuestId = questId, NpcId = npcId, SoundEditorUserId = soundEditorUserId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> UnlinkNpcFromQuestAsync(int questId, int npcId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM npc_quest WHERE quest_id = @QuestId AND npc_id = @NpcId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { QuestId = questId, NpcId = npcId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    private sealed class QuestContentRow
    {
        public int QuestId { get; init; }
        public string QuestName { get; init; } = string.Empty;
        public string QuestDegeneratedName { get; init; } = string.Empty;
        public int? WriterId { get; init; }
        public string? WriterName { get; init; }
    }

    private sealed class NpcContentRow
    {
        public int QuestId { get; init; }
        public int NpcId { get; init; }
        public string NpcName { get; init; } = string.Empty;
        public string NpcDegeneratedName { get; init; } = string.Empty;
        public int? VoiceActorId { get; init; }
        public string? VoiceActorName { get; init; }
        public int? SoundEditorId { get; init; }
        public string? SoundEditorName { get; init; }
        public int RecordingCount { get; init; }
    }
}
