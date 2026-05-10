using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Accounts;

namespace VoW.Api.Repositories;

public sealed class AccountRepository(IConfiguration configuration) : IAccountRepository
{
    private readonly string storageBaseUrl = NormalizeStorageBaseUrl(
        configuration["Storage:BaseUrl"] ?? "https://vow.blob.core.windows.net/vow-dynamic/");

    public async Task<IReadOnlyCollection<AccountRole>> GetRolesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT discord_role_id AS Id, name AS Name, color AS Color, weight AS Weight
            FROM discord_role
            ORDER BY weight DESC, name;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<AccountRole>(command)).AsList();
    }

    public async Task<AccountSearchPage> SearchAsync(AccountSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var whereSql = string.Empty;
        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            whereSql = """
                WHERE u.display_name LIKE @Query
                   OR u.email LIKE @Query
                   OR u.discord LIKE @Query
                   OR u.youtube LIKE @Query
                   OR u.twitter LIKE @Query
                   OR u.castingcallclub LIKE @Query
                   OR CAST(u.discord_id AS CHAR) = @ExactQuery
                   OR CAST(u.user_id AS CHAR) = @ExactQuery
                """;
            parameters.Add("Query", $"%{criteria.Query}%");
            parameters.Add("ExactQuery", criteria.Query);
        }

        parameters.Add("PageSize", criteria.PageSize);
        parameters.Add("Offset", (criteria.Page - 1) * criteria.PageSize);

        var countSql = $"SELECT COUNT(*) FROM user u {whereSql};";
        var userSql = $"""
            SELECT
                u.user_id AS UserId,
                u.display_name AS DisplayName,
                u.picture AS Picture,
                u.picture_type AS PictureType,
                CAST(u.discord_id AS CHAR) AS DiscordId,
                u.email AS Email,
                u.discord AS Discord,
                u.youtube AS Youtube,
                u.twitter AS Twitter,
                u.castingcallclub AS CastingCallClub
            FROM user u
            {whereSql}
            ORDER BY u.user_id
            LIMIT @PageSize OFFSET @Offset;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken);
        var total = await connection.ExecuteScalarAsync<int>(command);

        command = new CommandDefinition(userSql, parameters, cancellationToken: cancellationToken);
        var users = (await connection.QueryAsync<AccountRow>(command)).AsList();
        if (users.Count == 0)
        {
            return new AccountSearchPage(total, criteria.Page, criteria.PageSize, []);
        }

        var rolesByUser = await GetRoleIdsByUserAsync(
            connection,
            users.Select(user => user.UserId).ToArray(),
            cancellationToken);

        return new AccountSearchPage(
            total,
            criteria.Page,
            criteria.PageSize,
            users.Select(user =>
            {
                var pictureType = ParsePictureType(user.PictureType);
                return new AccountSummary(
                    user.UserId,
                    user.DisplayName,
                    user.Picture,
                    pictureType,
                    AvatarUrl(user.Picture, pictureType),
                    DefaultAvatarUrl(),
                    user.Email,
                    user.Discord,
                    user.Youtube,
                    user.Twitter,
                    user.CastingCallClub,
                    rolesByUser.GetValueOrDefault(user.UserId, []));
            }).ToArray());
    }

    public async Task<AccountDetails?> GetAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                user_id AS UserId,
                display_name AS DisplayName,
                picture AS Picture,
                picture_type AS PictureType,
                CAST(discord_id AS CHAR) AS DiscordId,
                email AS Email,
                public_email AS PublicEmail,
                discord AS Discord,
                youtube AS Youtube,
                twitter AS Twitter,
                castingcallclub AS CastingCallClub,
                bio AS Bio,
                lore AS Lore,
                system_admin AS SystemAdmin
            FROM user
            WHERE user_id = @UserId;
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var user = await connection.QuerySingleOrDefaultAsync<AccountDetailsRow>(command);
        if (user is null)
        {
            return null;
        }

        var rolesByUser = await GetRoleIdsByUserAsync(connection, [userId], cancellationToken);
        var pictureType = ParsePictureType(user.PictureType);
        return new AccountDetails(
            user.UserId,
            user.DisplayName,
            user.Picture,
            pictureType,
            AvatarUrl(user.Picture, pictureType),
            DefaultAvatarUrl(),
            user.DiscordId,
            user.Email,
            user.PublicEmail != 0,
            user.Discord,
            user.Youtube,
            user.Twitter,
            user.CastingCallClub,
            user.Bio,
            user.Lore,
            user.SystemAdmin != 0,
            rolesByUser.GetValueOrDefault(userId, []));
    }

    public async Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM user WHERE user_id = @UserId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> IsSystemAdminAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM user WHERE user_id = @UserId AND system_admin <> 0;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> DisplayNameExistsAsync(int exceptUserId, string displayName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM user
            WHERE UPPER(display_name) = UPPER(@DisplayName)
              AND user_id <> @ExceptUserId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, DisplayName = displayName }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> EmailExistsAsync(int exceptUserId, string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM user
            WHERE email = @Email
              AND user_id <> @ExceptUserId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, Email = email }, cancellationToken: cancellationToken);
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

    public async Task<bool> SocialExistsAsync(int exceptUserId, string column, string value, CancellationToken cancellationToken)
    {
        if (!AllowedSocialColumns.Contains(column))
        {
            throw new ArgumentException("Unsupported social column.", nameof(column));
        }

        var sql = $"""
            SELECT COUNT(*)
            FROM user
            WHERE UPPER({column}) = UPPER(@Value)
              AND user_id <> @ExceptUserId;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { ExceptUserId = exceptUserId, Value = value }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command) > 0;
    }

    public async Task<bool> UpdateAsync(int userId, UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        var sql = command.PasswordHash is null
            ? """
                UPDATE user
                SET discord_id = @DiscordId,
                    email = @Email,
                    display_name = @DisplayName,
                    bio = @Bio,
                    lore = @Lore,
                    discord = @Discord,
                    youtube = @Youtube,
                    twitter = @Twitter,
                    castingcallclub = @CastingCallClub,
                    public_email = COALESCE(@PublicEmail, public_email)
                WHERE user_id = @UserId;
                """
            : """
                UPDATE user
                SET discord_id = @DiscordId,
                    email = @Email,
                    password = @PasswordHash,
                    display_name = @DisplayName,
                    bio = @Bio,
                    lore = @Lore,
                    discord = @Discord,
                    youtube = @Youtube,
                    twitter = @Twitter,
                    castingcallclub = @CastingCallClub,
                    public_email = COALESCE(@PublicEmail, public_email)
                WHERE user_id = @UserId;
                """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var definition = new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                command.DisplayName,
                command.PasswordHash,
                command.DiscordId,
                command.Email,
                PublicEmail = command.PublicEmail is null ? (int?)null : command.PublicEmail.Value ? 1 : 0,
                command.Discord,
                command.Youtube,
                command.Twitter,
                command.CastingCallClub,
                command.Bio,
                command.Lore,
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(definition) > 0;
    }

    public async Task<int> InsertAsync(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO user (display_name, password, discord_id, discord, castingcallclub, force_password_change)
            VALUES (@DisplayName, @PasswordHash, @DiscordId, @Discord, @CastingCallClub, 1);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var definition = new CommandDefinition(
            sql,
            new
            {
                command.DisplayName,
                command.PasswordHash,
                command.DiscordId,
                command.Discord,
                command.CastingCallClub,
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(definition);
    }

    public async Task<bool> ReplaceRolesAsync(int userId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var existsCommand = new CommandDefinition(
                "SELECT COUNT(*) FROM user WHERE user_id = @UserId;",
                new { UserId = userId },
                transaction,
                cancellationToken: cancellationToken);
            if (await connection.ExecuteScalarAsync<int>(existsCommand) == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

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

    public async Task<bool> SetAvatarAsync(int userId, string picture, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE user SET picture = @Picture, picture_type = @PictureType WHERE user_id = @UserId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, Picture = picture, PictureType = ToDatabaseValue(PictureType.Manual) },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> ClearAvatarAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE user SET picture = DEFAULT, picture_type = @PictureType WHERE user_id = @UserId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, PictureType = ToDatabaseValue(PictureType.Default) },
            cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE user
            SET password = @PasswordHash,
                force_password_change = 1
            WHERE user_id = @UserId
              AND system_admin = 0;
            """;
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> DeleteAsync(int userId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM user WHERE user_id = @UserId;";
        await using var connection = new MySqlConnection(DatabaseSettings.GetWebsiteConnectionString(configuration));
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command) > 0;
    }

    private static async Task<IReadOnlyDictionary<int, IReadOnlyCollection<int>>> GetRoleIdsByUserAsync(
        MySqlConnection connection,
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id AS UserId, discord_role_id AS RoleId
            FROM user_discord_role
            WHERE user_id IN @UserIds
            ORDER BY user_id;
            """;

        var command = new CommandDefinition(sql, new { UserIds = userIds }, cancellationToken: cancellationToken);
        var rows = (await connection.QueryAsync<UserRoleRow>(command)).AsList();
        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<int>)group.Select(row => row.RoleId).Distinct().ToArray());
    }

    private static readonly HashSet<string> AllowedSocialColumns =
    [
        "discord",
        "youtube",
        "twitter",
        "castingcallclub"
    ];

    private string AvatarUrl(string picture, PictureType pictureType) =>
        pictureType == PictureType.Default
            ? DefaultAvatarUrl()
            : $"{storageBaseUrl}avatars/{picture}";

    private string DefaultAvatarUrl() => $"{storageBaseUrl}avatars/default.png";

    private static PictureType ParsePictureType(string value) =>
        value switch
        {
            "default" => PictureType.Default,
            "discord" => PictureType.Discord,
            "manual" => PictureType.Manual,
            _ => throw new InvalidOperationException($"Unknown picture type '{value}'.")
        };

    private static string ToDatabaseValue(PictureType pictureType) =>
        pictureType switch
        {
            PictureType.Default => "default",
            PictureType.Discord => "discord",
            PictureType.Manual => "manual",
            _ => throw new ArgumentOutOfRangeException(nameof(pictureType), pictureType, "Unknown picture type.")
        };

    private static string NormalizeStorageBaseUrl(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";

    private sealed class AccountRow
    {
        public AccountRow()
        {
        }

        public int UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;

        public string? DiscordId { get; set; }

        public string? Email { get; set; }

        public string? Discord { get; set; }

        public string? Youtube { get; set; }

        public string? Twitter { get; set; }

        public string? CastingCallClub { get; set; }
    }

    private sealed class AccountDetailsRow
    {
        public AccountDetailsRow()
        {
        }

        public int UserId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Picture { get; set; } = string.Empty;

        public string PictureType { get; set; } = string.Empty;

        public string? DiscordId { get; set; }

        public string? Email { get; set; }

        public sbyte PublicEmail { get; set; }

        public string? Discord { get; set; }

        public string? Youtube { get; set; }

        public string? Twitter { get; set; }

        public string? CastingCallClub { get; set; }

        public string? Bio { get; set; }

        public string? Lore { get; set; }

        public sbyte SystemAdmin { get; set; }
    }

    private sealed class UserRoleRow
    {
        public UserRoleRow()
        {
        }

        public int UserId { get; set; }

        public int RoleId { get; set; }
    }
}
