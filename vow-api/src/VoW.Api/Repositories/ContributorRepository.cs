using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Contributors;

namespace VoW.Api.Repositories;

public sealed class ContributorRepository(IConfiguration configuration) : IContributorRepository
{
    private readonly AvatarUrlResolver avatarUrls = new(configuration);

    private MySqlConnection Connect() => new(DatabaseSettings.GetWebsiteConnectionString(configuration));

    public async Task<ContributorPage> GetContributorsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        // Only people with at least one Discord role are credited, matching the legacy inner joins.
        const string countSql = """
            SELECT COUNT(DISTINCT u.user_id)
            FROM user u
            JOIN user_discord_role udr ON udr.user_id = u.user_id
            JOIN discord_role dr ON dr.discord_role_id = udr.discord_role_id;
            """;

        // Ordering by the highest role weight FIRST is a deliberate change from the legacy page,
        // which ordered by the summed weight and only started a new section when the top role
        // changed - so one role could open several sections. Infinite scroll needs each role group
        // to be contiguous across page boundaries, and display_name + user_id make the order total
        // so that paging can neither skip nor repeat a contributor.
        const string pageSql = """
            SELECT
                u.user_id AS UserId,
                u.display_name AS DisplayName,
                u.picture AS Picture,
                u.picture_type AS PictureType,
                u.lore AS Lore,
                (
                    SELECT dr2.discord_role_id
                    FROM user_discord_role udr2
                    JOIN discord_role dr2 ON dr2.discord_role_id = udr2.discord_role_id
                    WHERE udr2.user_id = u.user_id
                    ORDER BY dr2.weight DESC, dr2.discord_role_id
                    LIMIT 1
                ) AS TopRoleId,
                MAX(dr.weight) AS TopRoleWeight,
                SUM(dr.weight) AS RolesWeight
            FROM user u
            JOIN user_discord_role udr ON udr.user_id = u.user_id
            JOIN discord_role dr ON dr.discord_role_id = udr.discord_role_id
            GROUP BY u.user_id, u.display_name, u.picture, u.picture_type, u.lore
            ORDER BY TopRoleWeight DESC, TopRoleId, u.display_name, u.user_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = Connect();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, cancellationToken: cancellationToken));

        var rows = (await connection.QueryAsync<ContributorRow>(new CommandDefinition(
            pageSql,
            // Widened before multiplying: page is only bounded above by int.MaxValue, and an
            // overflowed offset would reach MySQL negative.
            new { PageSize = pageSize, Offset = (long)(page - 1) * pageSize },
            cancellationToken: cancellationToken))).AsList();

        if (rows.Count == 0)
        {
            return new ContributorPage(total, page, pageSize, []);
        }

        var rolesByUser = await GetRolesByUserAsync(
            connection,
            rows.Select(row => row.UserId).ToArray(),
            cancellationToken);

        return new ContributorPage(
            total,
            page,
            pageSize,
            rows.Select(row => new ContributorSummary(
                row.UserId,
                row.DisplayName,
                avatarUrls.AvatarUrl(row.Picture, row.PictureType),
                avatarUrls.DefaultAvatarUrl(),
                NullIfEmpty(row.Lore),
                TopRole(rolesByUser, row.UserId, row.TopRoleId),
                rolesByUser.GetValueOrDefault(row.UserId, []))).ToArray());
    }

    public async Task<ContributorProfile?> GetProfileAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                display_name AS DisplayName,
                picture AS Picture,
                picture_type AS PictureType,
                lore AS Lore,
                bio AS Bio,
                email AS Email,
                public_email AS PublicEmail,
                discord AS Discord,
                youtube AS Youtube,
                twitter AS Twitter,
                castingcallclub AS CastingCallClub
            FROM user
            WHERE user_id = @UserId;
            """;

        await using var connection = Connect();
        var row = await connection.QuerySingleOrDefaultAsync<ProfileRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var rolesByUser = await GetRolesByUserAsync(connection, [userId], cancellationToken);
        var publicEmail = row.PublicEmail != 0;
        return new ContributorProfile(
            row.UserId,
            row.DisplayName,
            avatarUrls.AvatarUrl(row.Picture, row.PictureType),
            avatarUrls.DefaultAvatarUrl(),
            NullIfEmpty(row.Lore),
            NullIfEmpty(row.Bio),
            // An address the contributor has not made public never leaves the server.
            publicEmail ? NullIfEmpty(row.Email) : null,
            publicEmail,
            NullIfEmpty(row.Discord),
            NullIfEmpty(row.Youtube),
            NullIfEmpty(row.Twitter),
            NullIfEmpty(row.CastingCallClub),
            rolesByUser.GetValueOrDefault(userId, []));
    }

    public async Task<IReadOnlyCollection<int>?> GetVoicedNpcIdsAsync(int userId, CancellationToken cancellationToken)
    {
        // The EXISTS mirrors the recording join in GetVoicedNpcsAsync below, so the votes answer
        // covers exactly the characters the profile page shows and no others.
        const string sql = """
            SELECT n.npc_id
            FROM npc n
            WHERE n.voice_actor_id = @UserId
              AND EXISTS (
                  SELECT 1
                  FROM recording r
                  WHERE r.npc_id = n.npc_id AND (r.archived = FALSE OR n.archived = TRUE)
              );
            """;

        await using var connection = Connect();
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM user WHERE user_id = @UserId);",
            new { UserId = userId },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return null;
        }

        return (await connection.QueryAsync<int>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<VoicedNpc>> GetVoicedNpcsAsync(int userId, CancellationToken cancellationToken)
    {
        // The legacy page counts comments with a correlated subquery per NPC; grouped left joins
        // keep this to one pass. Recording visibility mirrors the legacy rule: archived recordings
        // stay hidden unless the whole NPC is archived, in which case they are all that is left.
        const string npcSql = """
            SELECT
                n.npc_id AS NpcId,
                n.name AS NpcName,
                n.archived AS Archived,
                n.upvotes AS Upvotes,
                n.downvotes AS Downvotes,
                COALESCE(c.comment_count, 0) AS CommentCount,
                r.recording_count AS RecordingCount
            FROM npc n
            JOIN (
                SELECT npc_id, COUNT(*) AS recording_count
                FROM recording
                JOIN npc USING (npc_id)
                WHERE recording.archived = FALSE OR npc.archived = TRUE
                GROUP BY npc_id
            ) r ON r.npc_id = n.npc_id
            LEFT JOIN (
                SELECT npc_id, COUNT(*) AS comment_count
                FROM comment
                GROUP BY npc_id
            ) c ON c.npc_id = n.npc_id
            WHERE n.voice_actor_id = @UserId
            ORDER BY n.name, n.npc_id;
            """;

        const string questSql = """
            SELECT DISTINCT
                r.npc_id AS NpcId,
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName
            FROM recording r
            JOIN quest q ON q.quest_id = r.quest_id
            JOIN npc n ON n.npc_id = r.npc_id
            WHERE r.npc_id IN @NpcIds AND (r.archived = FALSE OR n.archived = TRUE)
            ORDER BY q.name, q.quest_id;
            """;

        await using var connection = Connect();
        var npcs = (await connection.QueryAsync<VoicedNpcRow>(
            new CommandDefinition(npcSql, new { UserId = userId }, cancellationToken: cancellationToken))).AsList();
        if (npcs.Count == 0)
        {
            return [];
        }

        var appearances = (await connection.QueryAsync<NpcQuestRow>(new CommandDefinition(
            questSql,
            new { NpcIds = npcs.Select(npc => npc.NpcId).ToArray() },
            cancellationToken: cancellationToken))).AsList();

        var questsByNpc = appearances
            .GroupBy(row => row.NpcId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<NpcQuestAppearance>)group
                    .Select(row => new NpcQuestAppearance(row.QuestId, row.QuestName, row.QuestDegeneratedName))
                    .ToArray());

        return npcs.Select(npc => new VoicedNpc(
            npc.NpcId,
            npc.NpcName,
            npc.Archived != 0,
            npc.Upvotes,
            npc.Downvotes,
            npc.CommentCount,
            npc.RecordingCount,
            questsByNpc.GetValueOrDefault(npc.NpcId, []))).ToArray();
    }

    public async Task<IReadOnlyCollection<WrittenQuest>> GetWrittenQuestsAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                quest_id AS QuestId,
                name AS QuestName,
                degenerated_name AS QuestDegeneratedName
            FROM quest
            WHERE writer = @UserId
            ORDER BY name, quest_id;
            """;

        await using var connection = Connect();
        return (await connection.QueryAsync<WrittenQuest>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<EditedQuest>> GetEditedQuestsAsync(int userId, CancellationToken cancellationToken)
    {
        // Returned grouped by quest. The legacy page emitted one flat row per (quest, NPC) pair,
        // which repeated the quest name once per NPC.
        const string sql = """
            SELECT
                q.quest_id AS QuestId,
                q.name AS QuestName,
                q.degenerated_name AS QuestDegeneratedName,
                n.npc_id AS NpcId,
                n.name AS NpcName
            FROM npc_quest nq
            JOIN quest q ON q.quest_id = nq.quest_id
            JOIN npc n ON n.npc_id = nq.npc_id
            WHERE nq.editor = @UserId
            ORDER BY q.name, q.quest_id, nq.sorting_order, n.name;
            """;

        await using var connection = Connect();
        var rows = (await connection.QueryAsync<EditedNpcRow>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).AsList();

        return rows
            .GroupBy(row => (row.QuestId, row.QuestName, row.QuestDegeneratedName))
            .Select(group => new EditedQuest(
                group.Key.QuestId,
                group.Key.QuestName,
                group.Key.QuestDegeneratedName,
                group.Select(row => new EditedNpc(row.NpcId, row.NpcName)).ToArray()))
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<int, IReadOnlyCollection<ContributorRole>>> GetRolesByUserAsync(
        MySqlConnection connection,
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                udr.user_id AS UserId,
                dr.discord_role_id AS Id,
                dr.name AS Name,
                dr.color AS Color,
                dr.weight AS Weight
            FROM user_discord_role udr
            JOIN discord_role dr ON dr.discord_role_id = udr.discord_role_id
            WHERE udr.user_id IN @UserIds
            ORDER BY udr.user_id, dr.weight DESC, dr.discord_role_id;
            """;

        var rows = (await connection.QueryAsync<UserRoleRow>(
            new CommandDefinition(sql, new { UserIds = userIds }, cancellationToken: cancellationToken))).AsList();

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<ContributorRole>)group
                    .Select(row => new ContributorRole(row.Id, row.Name, row.Color, row.Weight))
                    .ToArray());
    }

    /// <summary>
    /// The role a contributor is credited under. Returned explicitly rather than left as
    /// "the first of the roles list", which would depend on a tiebreak the client cannot see.
    /// </summary>
    private static ContributorRole TopRole(
        IReadOnlyDictionary<int, IReadOnlyCollection<ContributorRole>> rolesByUser,
        int userId,
        int topRoleId)
    {
        var roles = rolesByUser.GetValueOrDefault(userId, []);
        return roles.FirstOrDefault(role => role.Id == topRoleId)
            ?? roles.First();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class ContributorRow
    {
        public int UserId { get; set; }

        public int TopRoleId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;

        public string? Lore { get; set; }
    }

    private sealed class ProfileRow
    {
        public int UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;

        public string? Lore { get; set; }

        public string? Bio { get; set; }

        public string? Email { get; set; }

        public sbyte PublicEmail { get; set; }

        public string? Discord { get; set; }

        public string? Youtube { get; set; }

        public string? Twitter { get; set; }

        public string? CastingCallClub { get; set; }
    }

    private sealed class VoicedNpcRow
    {
        public int NpcId { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public sbyte Archived { get; set; }

        public int Upvotes { get; set; }

        public int Downvotes { get; set; }

        public int CommentCount { get; set; }

        public int RecordingCount { get; set; }
    }

    private sealed class NpcQuestRow
    {
        public int NpcId { get; set; }

        public int QuestId { get; set; }

        public string QuestName { get; set; } = string.Empty;

        public string QuestDegeneratedName { get; set; } = string.Empty;
    }

    private sealed class EditedNpcRow
    {
        public int QuestId { get; set; }

        public string QuestName { get; set; } = string.Empty;

        public string QuestDegeneratedName { get; set; } = string.Empty;

        public int NpcId { get; set; }

        public string NpcName { get; set; } = string.Empty;
    }

    private sealed class UserRoleRow
    {
        public int UserId { get; set; }

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;

        public int Weight { get; set; }
    }
}
