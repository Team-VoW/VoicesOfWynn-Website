using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Casting;

public interface ICastingBotService
{
    Task<BotCastingRoundResponse> EnsureRoundAsync(BotEnsureCastingRoundRequest request, CancellationToken cancellationToken);

    Task<CastingResult<BotAuditionResponse>> AddAuditionAsync(
        int roundId,
        BotUploadAuditionRequest request,
        Stream audio,
        CancellationToken cancellationToken);
}

/// <summary>
/// What VowBot's /setuppoll calls for a Discord casting: one draft round per quest, then one upload per
/// audition thread. Everything is keyed so the command can be re-run after new auditions come in.
/// </summary>
public sealed class CastingBotService(
    ICastingRoundRepository rounds,
    IUserRepository users,
    CastingAuditionIngestService ingest,
    IConfiguration configuration) : ICastingBotService
{
    public async Task<BotCastingRoundResponse> EnsureRoundAsync(
        BotEnsureCastingRoundRequest request,
        CancellationToken cancellationToken)
    {
        var questName = request.QuestName.Trim();
        var round = await rounds.FindActiveRoundBySourceAsync(CastingSource.Discord, questName, cancellationToken);
        var created = round is null;
        var roundId = round?.Id ?? await rounds.CreateRoundAsync(
            new NewCastingRound(
                new CastingRoundDetails(questName, null, null),
                CastingSource.Discord,
                questName,
                null),
            cancellationToken);

        foreach (var name in request.Characters.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct())
        {
            if (await rounds.FindCharacterByNameAsync(roundId, name, cancellationToken) is null)
            {
                await rounds.CreateCharacterAsync(roundId, new CastingCharacterDetails(name, questName, null), cancellationToken);
            }
        }

        return new BotCastingRoundResponse(roundId, created, AdminUrl(roundId));
    }

    public async Task<CastingResult<BotAuditionResponse>> AddAuditionAsync(
        int roundId,
        BotUploadAuditionRequest request,
        Stream audio,
        CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return CastingResult.NotFound();
        }

        var characterName = request.CharacterName.Trim();
        var character = await rounds.FindCharacterByNameAsync(roundId, characterName, cancellationToken);
        if (character is null)
        {
            var id = await rounds.CreateCharacterAsync(
                roundId, new CastingCharacterDetails(characterName, round.SourceRef, null), cancellationToken);
            character = id is null
                ? await rounds.FindCharacterByNameAsync(roundId, characterName, cancellationToken)
                : await rounds.GetCharacterAsync(id.Value, cancellationToken);
            if (character is null)
            {
                return CastingResult.Invalid("characterName", "Could not create the character.");
            }
        }

        int? userId = null;
        if (!string.IsNullOrWhiteSpace(request.DiscordUserId))
        {
            userId = (await users.GetByDiscordIdAsync(request.DiscordUserId.Trim(), cancellationToken))?.UserId;
        }

        try
        {
            var result = await ingest.AddAsync(
                character,
                new AuditionSubmission(request.AuditioneeName, userId, $"discord:{request.DiscordThreadId.Trim()}"),
                audio,
                cancellationToken);
            return CastingResult<BotAuditionResponse>.Success(new BotAuditionResponse(result.AuditionId, result.Created));
        }
        catch (AudioTranscodeException ex)
        {
            return CastingResult.Invalid("file", ex.Message);
        }
    }

    private string AdminUrl(int roundId)
    {
        var appBase = configuration["APP_BASE_URL"] ?? configuration["CORS_ORIGIN"] ?? "https://app.voicesofwynn.com";
        return $"{appBase.TrimEnd('/')}/admin/casting/{roundId}";
    }
}
