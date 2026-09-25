using Dapper;
using MySqlConnector;
using VoW.Api.Domain.Casting;

namespace VoW.Api.Repositories;

public sealed class CastingRoundRepository(IConfiguration configuration) : ICastingRoundRepository
{
    private const int DuplicateKeyError = 1062;
    private const int DeadlockError = 1213;

    private const string RoundColumns = """
        round_id AS Id,
        name AS Name,
        description AS Description,
        status AS Status,
        source AS Source,
        source_ref AS SourceRef,
        voting_closes_at AS VotingClosesAt,
        import_status AS ImportStatus,
        import_message AS ImportMessage,
        created_by AS CreatedBy,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt
        """;

    private const string CharacterColumns = """
        character_id AS Id,
        round_id AS RoundId,
        name AS Name,
        quest_name AS QuestName,
        direction AS Direction,
        sort_order AS SortOrder,
        winner_audition_id AS WinnerAuditionId
        """;

    private const string AuditionColumns = """
        a.audition_id AS Id,
        a.character_id AS CharacterId,
        a.`number` AS Number,
        a.auditionee_name AS AuditioneeName,
        a.auditionee_user_id AS AuditioneeUserId,
        a.source_ref AS SourceRef,
        a.audio_blob_path AS AudioBlobPath,
        a.duration_seconds AS DurationSeconds,
        a.created_at AS CreatedAt
        """;

    private sealed record RoundRow(
        int Id,
        string Name,
        string? Description,
        string Status,
        string Source,
        string? SourceRef,
        DateTime? VotingClosesAt,
        string ImportStatus,
        string? ImportMessage,
        int? CreatedBy,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private sealed record AuditionRow(
        int Id,
        int CharacterId,
        int Number,
        string AuditioneeName,
        int? AuditioneeUserId,
        string? SourceRef,
        string AudioBlobPath,
        double? DurationSeconds,
        DateTime CreatedAt);

    public async Task<IReadOnlyList<CastingRound>> GetRoundsAsync(
        IReadOnlyCollection<CastingRoundStatus> statuses,
        CancellationToken cancellationToken)
    {
        if (statuses.Count == 0)
        {
            return [];
        }

        var sql = $"""
            SELECT {RoundColumns}
            FROM casting_round
            WHERE status IN @Statuses
            ORDER BY created_at DESC, round_id DESC;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<RoundRow>(new CommandDefinition(
            sql,
            new { Statuses = statuses.Select(CastingEnumMapping.ToDb).ToArray() },
            cancellationToken: cancellationToken));
        return rows.Select(ToRound).ToList();
    }

    public async Task<CastingRound?> GetRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {RoundColumns} FROM casting_round WHERE round_id = @RoundId;";

        await using var connection = Open();
        var row = await connection.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
        return row is null ? null : ToRound(row);
    }

    public async Task<CastingRound?> FindRoundBySourceAsync(
        CastingSource source,
        string sourceRef,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {RoundColumns}
            FROM casting_round
            WHERE source = @Source AND source_ref = @SourceRef
            ORDER BY created_at DESC, round_id DESC
            LIMIT 1;
            """;

        await using var connection = Open();
        var row = await connection.QuerySingleOrDefaultAsync<RoundRow>(new CommandDefinition(
            sql,
            new { Source = CastingEnumMapping.ToDb(source), SourceRef = sourceRef },
            cancellationToken: cancellationToken));
        return row is null ? null : ToRound(row);
    }

    public async Task<int> CreateRoundAsync(NewCastingRound round, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO casting_round (name, description, source, source_ref, voting_closes_at, created_by)
            VALUES (@Name, @Description, @Source, @SourceRef, @VotingClosesAt, @CreatedBy);
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = Open();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
        {
            round.Details.Name,
            round.Details.Description,
            Source = CastingEnumMapping.ToDb(round.Source),
            round.SourceRef,
            round.Details.VotingClosesAt,
            round.CreatedBy
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateRoundAsync(int roundId, CastingRoundDetails details, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_round
            SET name = @Name,
                description = @Description,
                voting_closes_at = @VotingClosesAt
            WHERE round_id = @RoundId;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            RoundId = roundId,
            details.Name,
            details.Description,
            details.VotingClosesAt
        }, cancellationToken: cancellationToken));
    }

    public async Task SetRoundStatusAsync(int roundId, CastingRoundStatus status, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE casting_round SET status = @Status WHERE round_id = @RoundId;";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { RoundId = roundId, Status = CastingEnumMapping.ToDb(status) },
            cancellationToken: cancellationToken));
    }

    public async Task SetRoundSourceAsync(
        int roundId,
        CastingSource source,
        string? sourceRef,
        CancellationToken cancellationToken)
    {
        const string sql = "UPDATE casting_round SET source = @Source, source_ref = @SourceRef WHERE round_id = @RoundId;";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { RoundId = roundId, Source = CastingEnumMapping.ToDb(source), SourceRef = sourceRef },
            cancellationToken: cancellationToken));
    }

    public async Task SetImportStateAsync(
        int roundId,
        CastingImportStatus status,
        string? message,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_round
            SET import_status = @Status, import_message = @Message
            WHERE round_id = @RoundId;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            RoundId = roundId,
            Status = CastingEnumMapping.ToDb(status),
            Message = message is { Length: > 500 } ? message[..500] : message
        }, cancellationToken: cancellationToken));
    }

    public async Task FailRunningImportsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_round
            SET import_status = 'failed',
                import_message = 'The import was interrupted by an API restart. Start it again to continue.'
            WHERE import_status = 'running';
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task DeleteRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        // The winner foreign key points back at casting_audition, so clear it before the cascade runs.
        const string sql = """
            UPDATE casting_character SET winner_audition_id = NULL WHERE round_id = @RoundId;
            DELETE FROM casting_round WHERE round_id = @RoundId;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CastingCharacter>> GetCharactersAsync(int roundId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {CharacterColumns}
            FROM casting_character
            WHERE round_id = @RoundId
            ORDER BY sort_order, character_id;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<CastingCharacter>(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<CastingCharacter?> GetCharacterAsync(int characterId, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {CharacterColumns} FROM casting_character WHERE character_id = @CharacterId;";

        await using var connection = Open();
        return await connection.QuerySingleOrDefaultAsync<CastingCharacter>(new CommandDefinition(
            sql, new { CharacterId = characterId }, cancellationToken: cancellationToken));
    }

    public async Task<CastingCharacter?> FindCharacterByNameAsync(
        int roundId,
        string name,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT {CharacterColumns} FROM casting_character WHERE round_id = @RoundId AND name = @Name;";

        await using var connection = Open();
        return await connection.QuerySingleOrDefaultAsync<CastingCharacter>(new CommandDefinition(
            sql, new { RoundId = roundId, Name = name }, cancellationToken: cancellationToken));
    }

    public async Task<int?> CreateCharacterAsync(
        int roundId,
        CastingCharacterDetails details,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO casting_character (round_id, name, quest_name, direction, sort_order)
            SELECT @RoundId, @Name, @QuestName, @Direction, COALESCE(MAX(sort_order), 0) + 1
            FROM casting_character
            WHERE round_id = @RoundId;
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = Open();
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
            {
                RoundId = roundId,
                details.Name,
                details.QuestName,
                details.Direction
            }, cancellationToken: cancellationToken));
        }
        catch (MySqlException ex) when (ex.Number == DuplicateKeyError)
        {
            return null;
        }
    }

    public async Task<bool> UpdateCharacterAsync(
        int characterId,
        CastingCharacterDetails details,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_character
            SET name = @Name, quest_name = @QuestName, direction = @Direction
            WHERE character_id = @CharacterId;
            """;

        await using var connection = Open();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                CharacterId = characterId,
                details.Name,
                details.QuestName,
                details.Direction
            }, cancellationToken: cancellationToken));
            return true;
        }
        catch (MySqlException ex) when (ex.Number == DuplicateKeyError)
        {
            return false;
        }
    }

    public async Task DeleteCharacterAsync(int characterId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE casting_character SET winner_audition_id = NULL WHERE character_id = @CharacterId;
            DELETE FROM casting_character WHERE character_id = @CharacterId;
            """;

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { CharacterId = characterId }, cancellationToken: cancellationToken));
    }

    public async Task SetWinnerAsync(int characterId, int? auditionId, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE casting_character SET winner_audition_id = @AuditionId WHERE character_id = @CharacterId;";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { CharacterId = characterId, AuditionId = auditionId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CastingAudition>> GetAuditionsForRoundAsync(
        int roundId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {AuditionColumns}
            FROM casting_audition a
            JOIN casting_character c ON c.character_id = a.character_id
            WHERE c.round_id = @RoundId
            ORDER BY a.character_id, a.`number`;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<AuditionRow>(new CommandDefinition(
            sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
        return rows.Select(ToAudition).ToList();
    }

    public async Task<IReadOnlyList<CastingAudition>> GetAuditionsAsync(int characterId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {AuditionColumns}
            FROM casting_audition a
            WHERE a.character_id = @CharacterId
            ORDER BY a.`number`;
            """;

        await using var connection = Open();
        var rows = await connection.QueryAsync<AuditionRow>(new CommandDefinition(
            sql, new { CharacterId = characterId }, cancellationToken: cancellationToken));
        return rows.Select(ToAudition).ToList();
    }

    public async Task<CastingAudition?> GetAuditionAsync(int auditionId, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {AuditionColumns} FROM casting_audition a WHERE a.audition_id = @AuditionId;";

        await using var connection = Open();
        var row = await connection.QuerySingleOrDefaultAsync<AuditionRow>(new CommandDefinition(
            sql, new { AuditionId = auditionId }, cancellationToken: cancellationToken));
        return row is null ? null : ToAudition(row);
    }

    public async Task<CastingAudition?> FindAuditionBySourceAsync(
        int characterId,
        string sourceRef,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {AuditionColumns}
            FROM casting_audition a
            WHERE a.character_id = @CharacterId AND a.source_ref_hash = UNHEX(SHA2(@SourceRef, 256));
            """;

        await using var connection = Open();
        var row = await connection.QuerySingleOrDefaultAsync<AuditionRow>(new CommandDefinition(
            sql, new { CharacterId = characterId, SourceRef = sourceRef }, cancellationToken: cancellationToken));
        return row is null ? null : ToAudition(row);
    }

    public async Task<int?> CreateAuditionAsync(NewCastingAudition audition, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO casting_audition
                (character_id, `number`, auditionee_name, auditionee_user_id, source_ref, audio_blob_path, duration_seconds)
            SELECT @CharacterId, COALESCE(MAX(`number`), 0) + 1, @AuditioneeName, @AuditioneeUserId,
                   @SourceRef, @AudioBlobPath, @DurationSeconds
            FROM casting_audition
            WHERE character_id = @CharacterId;
            SELECT LAST_INSERT_ID();
            """;

        await using var connection = Open();
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    sql, audition, cancellationToken: cancellationToken));
            }
            catch (MySqlException ex) when (!cancellationToken.IsCancellationRequested &&
                                            ex.Number == DuplicateKeyError &&
                                            ex.Message.Contains("casting_audition_source", StringComparison.Ordinal))
            {
                return null;
            }
            catch (MySqlException ex) when (!cancellationToken.IsCancellationRequested &&
                                            attempt < maxAttempts &&
                                            (ex.Number == DeadlockError ||
                                             (ex.Number == DuplicateKeyError &&
                                              ex.Message.Contains("casting_audition_number", StringComparison.Ordinal))))
            {
                // Re-running the INSERT recomputes MAX(number) after the competing write.
            }
        }

        throw new InvalidOperationException("Audition insert exhausted its retry attempts.");
    }

    public async Task DeleteAuditionAsync(int auditionId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM casting_audition WHERE audition_id = @AuditionId;";

        await using var connection = Open();
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { AuditionId = auditionId }, cancellationToken: cancellationToken));
    }

    private MySqlConnection Open() => new(DatabaseSettings.GetWebsiteConnectionString(configuration));

    private static CastingRound ToRound(RoundRow row) => new(
        row.Id,
        row.Name,
        row.Description,
        CastingEnumMapping.RoundStatusFromDb(row.Status),
        CastingEnumMapping.SourceFromDb(row.Source),
        row.SourceRef,
        row.VotingClosesAt is { } closesAt ? AsUtc(closesAt) : null,
        CastingEnumMapping.ImportStatusFromDb(row.ImportStatus),
        row.ImportMessage,
        row.CreatedBy,
        AsUtc(row.CreatedAt),
        AsUtc(row.UpdatedAt));

    private static CastingAudition ToAudition(AuditionRow row) => new(
        row.Id,
        row.CharacterId,
        row.Number,
        row.AuditioneeName,
        row.AuditioneeUserId,
        row.SourceRef,
        row.AudioBlobPath,
        row.DurationSeconds,
        AsUtc(row.CreatedAt));

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
