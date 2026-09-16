using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Contents;

namespace VoW.Api.Repositories;

public sealed class ContentPageRepository(IConfiguration configuration) : IContentPageRepository
{
    private readonly AvatarUrlResolver avatarUrls = new(configuration);

    private MySqlConnection Connect() => new(DatabaseSettings.GetWebsiteConnectionString(configuration));

    public async Task<IReadOnlyCollection<QuestListItem>> GetQuestListAsync(CancellationToken cancellationToken)
    {
        // Ordered by name rather than by quest_id as the legacy page was: the index is meant to be
        // scanned and searched, and insertion order tells a visitor nothing.
        // The recording count is taken through npc_quest, not straight off recording.quest_id, so
        // it matches what the quest page can actually play: recordings left behind by an NPC that
        // has since been unlinked from the quest are counted by neither. Within that, visibility
        // follows the rule used everywhere else - archived recordings stay hidden unless the whole
        // NPC is archived, in which case they are all that is left.
        const string sql = """
            SELECT
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName,
                (
                    SELECT COUNT(*)
                    FROM npc_quest nq
                    WHERE nq.quest_id = q.quest_id
                ) AS NpcCount,
                (
                    SELECT COUNT(*)
                    FROM npc_quest nq
                    JOIN npc n ON n.npc_id = nq.npc_id
                    JOIN recording r ON r.npc_id = nq.npc_id AND r.quest_id = nq.quest_id
                    WHERE nq.quest_id = q.quest_id AND (r.archived = FALSE OR n.archived = TRUE)
                ) AS RecordingCount
            FROM quest q
            ORDER BY q.name, q.quest_id;
            """;

        await using var connection = Connect();
        var rows = await connection.QueryAsync<QuestListRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(row => new QuestListItem(
            row.QuestId,
            row.QuestName,
            row.QuestDegeneratedName,
            row.NpcCount,
            row.RecordingCount)).ToArray();
    }

    public async Task<QuestDetail?> GetQuestAsync(string degeneratedName, CancellationToken cancellationToken)
    {
        const string questSql = """
            SELECT
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName,
                w.user_id AS CreditUserId,
                w.display_name AS CreditDisplayName,
                w.picture AS CreditPicture,
                w.picture_type AS CreditPictureType
            FROM quest q
            LEFT JOIN user w ON w.user_id = q.writer
            WHERE q.degenerated_name = @DegeneratedName;
            """;

        // Counted with correlated subqueries rather than grouped joins: a quest has a handful of
        // NPCs, and both counts are served straight from an index on npc_id.
        const string npcSql = """
            SELECT
                n.npc_id AS NpcId,
                n.name AS NpcName,
                n.archived AS Archived,
                n.upvotes AS Upvotes,
                n.downvotes AS Downvotes,
                (
                    SELECT COUNT(*)
                    FROM comment c
                    WHERE c.npc_id = n.npc_id
                ) AS CommentCount,
                (
                    SELECT COUNT(*)
                    FROM recording r
                    WHERE r.npc_id = n.npc_id
                      AND r.quest_id = nq.quest_id
                      AND (r.archived = FALSE OR n.archived = TRUE)
                ) AS RecordingCount,
                va.user_id AS VoiceActorUserId,
                va.display_name AS VoiceActorDisplayName,
                va.picture AS VoiceActorPicture,
                va.picture_type AS VoiceActorPictureType,
                se.user_id AS SoundEditorUserId,
                se.display_name AS SoundEditorDisplayName,
                se.picture AS SoundEditorPicture,
                se.picture_type AS SoundEditorPictureType
            FROM npc_quest nq
            JOIN npc n ON n.npc_id = nq.npc_id
            LEFT JOIN user va ON va.user_id = n.voice_actor_id
            LEFT JOIN user se ON se.user_id = nq.editor
            WHERE nq.quest_id = @QuestId
            ORDER BY nq.sorting_order, n.name, n.npc_id;
            """;

        await using var connection = Connect();
        var quest = await connection.QuerySingleOrDefaultAsync<QuestRow>(new CommandDefinition(
            questSql,
            new { DegeneratedName = degeneratedName },
            cancellationToken: cancellationToken));
        if (quest is null)
        {
            return null;
        }

        var npcs = (await connection.QueryAsync<QuestNpcRow>(new CommandDefinition(
            npcSql,
            new { quest.QuestId },
            cancellationToken: cancellationToken))).AsList();

        return new QuestDetail(
            quest.QuestId,
            quest.QuestName,
            quest.QuestDegeneratedName,
            Credit(quest.CreditUserId, quest.CreditDisplayName, quest.CreditPicture, quest.CreditPictureType),
            npcs.Select(npc => new QuestNpc(
                npc.NpcId,
                npc.NpcName,
                npc.Archived != 0,
                npc.Upvotes,
                npc.Downvotes,
                npc.CommentCount,
                npc.RecordingCount,
                Credit(
                    npc.VoiceActorUserId,
                    npc.VoiceActorDisplayName,
                    npc.VoiceActorPicture,
                    npc.VoiceActorPictureType),
                Credit(
                    npc.SoundEditorUserId,
                    npc.SoundEditorDisplayName,
                    npc.SoundEditorPicture,
                    npc.SoundEditorPictureType))).ToArray());
    }

    public async Task<NpcDetail?> GetNpcAsync(int npcId, CancellationToken cancellationToken)
    {
        const string npcSql = """
            SELECT
                n.npc_id AS NpcId,
                n.name AS NpcName,
                n.archived AS Archived,
                n.upvotes AS Upvotes,
                n.downvotes AS Downvotes,
                (
                    SELECT COUNT(*)
                    FROM comment c
                    WHERE c.npc_id = n.npc_id
                ) AS CommentCount,
                (
                    SELECT COUNT(*)
                    FROM recording r
                    WHERE r.npc_id = n.npc_id AND (r.archived = FALSE OR n.archived = TRUE)
                ) AS RecordingCount,
                va.user_id AS CreditUserId,
                va.display_name AS CreditDisplayName,
                va.picture AS CreditPicture,
                va.picture_type AS CreditPictureType
            FROM npc n
            LEFT JOIN user va ON va.user_id = n.voice_actor_id
            WHERE n.npc_id = @NpcId;
            """;

        // The quest list comes from npc_quest, not from the recordings: an NPC that has been cast
        // but not yet recorded still belongs to its quests.
        const string questSql = """
            SELECT
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName,
                se.user_id AS CreditUserId,
                se.display_name AS CreditDisplayName,
                se.picture AS CreditPicture,
                se.picture_type AS CreditPictureType
            FROM npc_quest nq
            JOIN quest q ON q.quest_id = nq.quest_id
            LEFT JOIN user se ON se.user_id = nq.editor
            WHERE nq.npc_id = @NpcId
            ORDER BY q.name, q.quest_id;
            """;

        await using var connection = Connect();
        var npc = await connection.QuerySingleOrDefaultAsync<NpcRow>(new CommandDefinition(
            npcSql,
            new { NpcId = npcId },
            cancellationToken: cancellationToken));
        if (npc is null)
        {
            return null;
        }

        var quests = (await connection.QueryAsync<NpcQuestRow>(new CommandDefinition(
            questSql,
            new { NpcId = npcId },
            cancellationToken: cancellationToken))).AsList();

        return new NpcDetail(
            npc.NpcId,
            npc.NpcName,
            npc.Archived != 0,
            npc.Upvotes,
            npc.Downvotes,
            npc.CommentCount,
            npc.RecordingCount,
            Credit(npc.CreditUserId, npc.CreditDisplayName, npc.CreditPicture, npc.CreditPictureType),
            quests.Select(quest => new NpcQuestCredit(
                quest.QuestId,
                quest.QuestName,
                quest.QuestDegeneratedName,
                Credit(
                    quest.CreditUserId,
                    quest.CreditDisplayName,
                    quest.CreditPicture,
                    quest.CreditPictureType))).ToArray());
    }

    /// <summary>Builds a credit from an outer-joined user, which is absent when the role is uncast.</summary>
    private ContentCredit? Credit(int? userId, string? displayName, string? picture, string? pictureType)
    {
        if (userId is null || displayName is null || picture is null || pictureType is null)
        {
            return null;
        }

        return new ContentCredit(
            userId.Value,
            displayName,
            avatarUrls.AvatarUrl(picture, pictureType),
            avatarUrls.DefaultAvatarUrl());
    }

    /// <summary>The four columns an outer-joined credit contributes, shared by the row types below.</summary>
    private abstract class CreditColumns
    {
        public int? CreditUserId { get; set; }

        public string? CreditDisplayName { get; set; }

        public string? CreditPicture { get; set; }

        public string? CreditPictureType { get; set; }
    }

    private sealed class QuestRow : CreditColumns
    {
        public int QuestId { get; set; }

        public string QuestName { get; set; } = string.Empty;

        public string QuestDegeneratedName { get; set; } = string.Empty;
    }

    /// <summary>
    /// MySQL counts to BIGINT, which Dapper will not bind to a record's int parameters - hence a
    /// class with settable properties here rather than materializing the domain type directly.
    /// </summary>
    private sealed class QuestListRow
    {
        public int QuestId { get; set; }

        public string QuestName { get; set; } = string.Empty;

        public string QuestDegeneratedName { get; set; } = string.Empty;

        public int NpcCount { get; set; }

        public int RecordingCount { get; set; }
    }

    private sealed class QuestNpcRow
    {
        public int NpcId { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public sbyte Archived { get; set; }

        public int Upvotes { get; set; }

        public int Downvotes { get; set; }

        public int CommentCount { get; set; }

        public int RecordingCount { get; set; }

        public int? VoiceActorUserId { get; set; }

        public string? VoiceActorDisplayName { get; set; }

        public string? VoiceActorPicture { get; set; }

        public string? VoiceActorPictureType { get; set; }

        public int? SoundEditorUserId { get; set; }

        public string? SoundEditorDisplayName { get; set; }

        public string? SoundEditorPicture { get; set; }

        public string? SoundEditorPictureType { get; set; }
    }

    private sealed class NpcRow : CreditColumns
    {
        public int NpcId { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public sbyte Archived { get; set; }

        public int Upvotes { get; set; }

        public int Downvotes { get; set; }

        public int CommentCount { get; set; }

        public int RecordingCount { get; set; }
    }

    private sealed class NpcQuestRow : CreditColumns
    {
        public int QuestId { get; set; }

        public string QuestName { get; set; } = string.Empty;

        public string QuestDegeneratedName { get; set; } = string.Empty;
    }
}
