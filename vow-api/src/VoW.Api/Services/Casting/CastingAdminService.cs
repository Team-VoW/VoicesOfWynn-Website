using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Casting;

public interface ICastingAdminService
{
    Task<AdminCastingRoundListResponse> GetRoundsAsync(bool includeArchived, CancellationToken cancellationToken);

    Task<AdminCastingRoundDetailResponse?> GetRoundAsync(int roundId, CancellationToken cancellationToken);

    Task<CastingResult<int>> CreateRoundAsync(SaveCastingRoundRequest request, int? userId, CancellationToken cancellationToken);

    Task<CastingResult> UpdateRoundAsync(int roundId, SaveCastingRoundRequest request, CancellationToken cancellationToken);

    Task<CastingResult> SetStatusAsync(int roundId, CastingRoundStatus status, CancellationToken cancellationToken);

    Task<CastingResult> DeleteRoundAsync(int roundId, CancellationToken cancellationToken);

    Task<CastingResult<int>> CreateCharacterAsync(int roundId, SaveCastingCharacterRequest request, CancellationToken cancellationToken);

    Task<CastingResult> UpdateCharacterAsync(int characterId, SaveCastingCharacterRequest request, CancellationToken cancellationToken);

    Task<CastingResult> DeleteCharacterAsync(int characterId, CancellationToken cancellationToken);

    Task<CastingResult> SetWinnerAsync(int characterId, int? auditionId, CancellationToken cancellationToken);

    Task<CastingResult<int>> UploadAuditionAsync(int characterId, string auditioneeName, Stream audio, CancellationToken cancellationToken);

    Task<CastingResult> DeleteAuditionAsync(int auditionId, CancellationToken cancellationToken);

    Task<CastingResult> StartCccImportAsync(int roundId, string url, CancellationToken cancellationToken);
}

public sealed class CastingAdminService(
    ICastingRoundRepository rounds,
    ICastingAudioStorage storage,
    CastingAuditionIngestService ingest,
    ICastingImportQueue importQueue,
    TimeProvider timeProvider) : ICastingAdminService
{
    private static readonly IReadOnlyDictionary<CastingRoundStatus, CastingRoundStatus[]> AllowedTransitions =
        new Dictionary<CastingRoundStatus, CastingRoundStatus[]>
        {
            [CastingRoundStatus.Draft] = [CastingRoundStatus.Open],
            [CastingRoundStatus.Open] = [CastingRoundStatus.Draft, CastingRoundStatus.Closed],
            [CastingRoundStatus.Closed] = [CastingRoundStatus.Open, CastingRoundStatus.Archived],
            [CastingRoundStatus.Archived] = [CastingRoundStatus.Closed],
        };

    public static bool CanTransition(CastingRoundStatus from, CastingRoundStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public async Task<AdminCastingRoundListResponse> GetRoundsAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        CastingRoundStatus[] statuses = includeArchived
            ? [CastingRoundStatus.Draft, CastingRoundStatus.Open, CastingRoundStatus.Closed, CastingRoundStatus.Archived]
            : [CastingRoundStatus.Draft, CastingRoundStatus.Open, CastingRoundStatus.Closed];
        var list = await rounds.GetRoundsAsync(statuses, cancellationToken);

        var responses = new List<AdminCastingRoundResponse>(list.Count);
        foreach (var round in list)
        {
            var characters = await rounds.GetCharactersAsync(round.Id, cancellationToken);
            var auditions = await rounds.GetAuditionsForRoundAsync(round.Id, cancellationToken);
            responses.Add(ToResponse(round, characters.Count, auditions.Count));
        }

        return new AdminCastingRoundListResponse(responses);
    }

    public async Task<AdminCastingRoundDetailResponse?> GetRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return null;
        }

        var characters = await rounds.GetCharactersAsync(roundId, cancellationToken);
        var auditions = await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken);
        return new AdminCastingRoundDetailResponse(
            ToResponse(round, characters.Count, auditions.Count),
            characters.Select(c => new AdminCastingCharacterResponse(
                c.Id,
                c.Name,
                c.QuestName,
                c.Direction,
                c.WinnerAuditionId,
                auditions.Where(a => a.CharacterId == c.Id)
                    .Select(a => new AdminCastingAuditionResponse(
                        a.Id,
                        a.Number,
                        a.AuditioneeName,
                        a.AuditioneeUserId,
                        storage.GetReadUrl(a.AudioBlobPath).ToString(),
                        a.DurationSeconds))
                    .ToList()))
                .ToList());
    }

    public async Task<CastingResult<int>> CreateRoundAsync(
        SaveCastingRoundRequest request,
        int? userId,
        CancellationToken cancellationToken)
    {
        var id = await rounds.CreateRoundAsync(
            new NewCastingRound(ToDetails(request), CastingSource.Manual, null, userId),
            cancellationToken);
        return CastingResult<int>.Success(id);
    }

    public async Task<CastingResult> UpdateRoundAsync(
        int roundId,
        SaveCastingRoundRequest request,
        CancellationToken cancellationToken)
    {
        if (await rounds.GetRoundAsync(roundId, cancellationToken) is null)
        {
            return CastingResult.NotFound();
        }

        await rounds.UpdateRoundAsync(roundId, ToDetails(request), cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> SetStatusAsync(
        int roundId,
        CastingRoundStatus status,
        CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return CastingResult.NotFound();
        }

        if (round.Status == status)
        {
            return CastingResult.Success();
        }

        if (!CanTransition(round.Status, status))
        {
            return CastingResult.Invalid("status", $"A {round.Status.ToString().ToLowerInvariant()} round cannot become {status.ToString().ToLowerInvariant()}.");
        }

        if (status == CastingRoundStatus.Open)
        {
            if (round.VotingClosesAt is { } closesAt && closesAt <= timeProvider.GetUtcNow().UtcDateTime)
            {
                return CastingResult.Invalid("votingClosesAt", "The voting close date is in the past. Move it forward before opening the round.");
            }

            if ((await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken)).Count == 0)
            {
                return CastingResult.Invalid("status", "Add at least one audition before opening the round.");
            }
        }

        await rounds.SetRoundStatusAsync(roundId, status, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> DeleteRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return CastingResult.NotFound();
        }

        if (round.Status != CastingRoundStatus.Draft)
        {
            return CastingResult.Invalid("status", "Only draft rounds can be deleted. Archive finished rounds instead.");
        }

        if (round.ImportStatus == CastingImportStatus.Running)
        {
            return CastingResult.Invalid("status", "Wait for the running import to finish before deleting the round.");
        }

        var auditions = await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken);
        await rounds.DeleteRoundAsync(roundId, cancellationToken);
        foreach (var audition in auditions)
        {
            await storage.DeleteAsync(audition.AudioBlobPath, cancellationToken);
        }

        return CastingResult.Success();
    }

    public async Task<CastingResult<int>> CreateCharacterAsync(
        int roundId,
        SaveCastingCharacterRequest request,
        CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return CastingResult.NotFound();
        }

        var editable = CheckEditable(round);
        if (!editable.Succeeded)
        {
            return editable;
        }

        var id = await rounds.CreateCharacterAsync(roundId, ToDetails(request), cancellationToken);
        return id is null
            ? CastingResult.Invalid("name", "This round already has a character with that name.")
            : CastingResult<int>.Success(id.Value);
    }

    public async Task<CastingResult> UpdateCharacterAsync(
        int characterId,
        SaveCastingCharacterRequest request,
        CancellationToken cancellationToken)
    {
        var character = await rounds.GetCharacterAsync(characterId, cancellationToken);
        if (character is null)
        {
            return CastingResult.NotFound();
        }

        return await rounds.UpdateCharacterAsync(characterId, ToDetails(request), cancellationToken)
            ? CastingResult.Success()
            : CastingResult.Invalid("name", "This round already has a character with that name.");
    }

    public async Task<CastingResult> DeleteCharacterAsync(int characterId, CancellationToken cancellationToken)
    {
        var character = await rounds.GetCharacterAsync(characterId, cancellationToken);
        if (character is null)
        {
            return CastingResult.NotFound();
        }

        var editable = CheckEditable(await rounds.GetRoundAsync(character.RoundId, cancellationToken));
        if (!editable.Succeeded)
        {
            return editable;
        }

        var auditions = await rounds.GetAuditionsAsync(characterId, cancellationToken);
        await rounds.DeleteCharacterAsync(characterId, cancellationToken);
        foreach (var audition in auditions)
        {
            await storage.DeleteAsync(audition.AudioBlobPath, cancellationToken);
        }

        return CastingResult.Success();
    }

    public async Task<CastingResult> SetWinnerAsync(int characterId, int? auditionId, CancellationToken cancellationToken)
    {
        var character = await rounds.GetCharacterAsync(characterId, cancellationToken);
        if (character is null)
        {
            return CastingResult.NotFound();
        }

        if (auditionId is not null)
        {
            var audition = await rounds.GetAuditionAsync(auditionId.Value, cancellationToken);
            if (audition is null || audition.CharacterId != characterId)
            {
                return CastingResult.Invalid("auditionId", "That audition does not belong to this character.");
            }
        }

        await rounds.SetWinnerAsync(characterId, auditionId, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult<int>> UploadAuditionAsync(
        int characterId,
        string auditioneeName,
        Stream audio,
        CancellationToken cancellationToken)
    {
        var character = await rounds.GetCharacterAsync(characterId, cancellationToken);
        if (character is null)
        {
            return CastingResult.NotFound();
        }

        var editable = CheckEditable(await rounds.GetRoundAsync(character.RoundId, cancellationToken));
        if (!editable.Succeeded)
        {
            return editable;
        }

        try
        {
            var result = await ingest.AddAsync(character, new AuditionSubmission(auditioneeName, null, null), audio, cancellationToken);
            return CastingResult<int>.Success(result.AuditionId);
        }
        catch (AudioTranscodeException ex)
        {
            return CastingResult.Invalid("file", ex.Message);
        }
    }

    public async Task<CastingResult> DeleteAuditionAsync(int auditionId, CancellationToken cancellationToken)
    {
        var audition = await rounds.GetAuditionAsync(auditionId, cancellationToken);
        if (audition is null)
        {
            return CastingResult.NotFound();
        }

        var character = await rounds.GetCharacterAsync(audition.CharacterId, cancellationToken);
        var editable = CheckEditable(character is null ? null : await rounds.GetRoundAsync(character.RoundId, cancellationToken));
        if (!editable.Succeeded)
        {
            return editable;
        }

        await ingest.DeleteAsync(audition, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> StartCccImportAsync(int roundId, string url, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return CastingResult.NotFound();
        }

        var editable = CheckEditable(round);
        if (!editable.Succeeded)
        {
            return editable;
        }

        if (!CccClient.IsCccUrl(url.Trim()))
        {
            return CastingResult.Invalid("url", "Paste the https://www.castingcall.club link of the casting call.");
        }

        if (round.ImportStatus == CastingImportStatus.Running)
        {
            return CastingResult.Invalid("url", "An import is already running for this round.");
        }

        await rounds.SetImportStateAsync(roundId, CastingImportStatus.Running, "Queued…", cancellationToken);
        if (round.Source == CastingSource.Manual)
        {
            await rounds.SetRoundSourceAsync(roundId, CastingSource.Ccc, url.Trim(), cancellationToken);
        }

        if (!importQueue.TryEnqueue(new CccImportJob(roundId, url.Trim())))
        {
            await rounds.SetImportStateAsync(roundId, CastingImportStatus.Failed, "Too many imports are queued. Try again in a few minutes.", cancellationToken);
            return CastingResult.Invalid("url", "Too many imports are queued. Try again in a few minutes.");
        }

        return CastingResult.Success();
    }

    /// <summary>Closed and archived rounds are a record of what was voted on, so their line-up is frozen.</summary>
    private static CastingResult CheckEditable(CastingRound? round) => round switch
    {
        null => CastingResult.NotFound(),
        { Status: CastingRoundStatus.Closed or CastingRoundStatus.Archived } =>
            CastingResult.Invalid("status", "Reopen the round before changing its characters or auditions."),
        _ => CastingResult.Success()
    };

    private AdminCastingRoundResponse ToResponse(CastingRound round, int characterCount, int auditionCount) => new(
        round.Id,
        round.Name,
        round.Description,
        round.Status,
        round.Source,
        round.SourceRef,
        round.VotingClosesAt,
        round.IsVotingOpen(timeProvider.GetUtcNow().UtcDateTime),
        round.ImportStatus,
        round.ImportMessage,
        characterCount,
        auditionCount,
        round.CreatedAt,
        round.UpdatedAt);

    private static CastingRoundDetails ToDetails(SaveCastingRoundRequest request) => new(
        request.Name.Trim(),
        NullIfBlank(request.Description),
        request.VotingClosesAt is { } closesAt ? DateTime.SpecifyKind(closesAt.ToUniversalTime(), DateTimeKind.Utc) : null);

    private static CastingCharacterDetails ToDetails(SaveCastingCharacterRequest request) => new(
        request.Name.Trim(),
        NullIfBlank(request.QuestName),
        NullIfBlank(request.Direction));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
