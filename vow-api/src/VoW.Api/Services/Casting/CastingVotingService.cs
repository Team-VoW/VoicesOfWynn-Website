using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Casting;

public interface ICastingVotingService
{
    Task<CastingRoundListResponse> GetOpenRoundsAsync(int userId, CancellationToken cancellationToken);

    Task<CastingRoundDetailResponse?> GetRoundAsync(int roundId, int userId, CancellationToken cancellationToken);

    Task<CastingMyPicksResponse?> GetMyPicksAsync(int roundId, int userId, CancellationToken cancellationToken);

    Task<CastingAuditionListResponse?> GetAuditionsAsync(int characterId, int userId, CancellationToken cancellationToken);

    Task<CastingResult> VoteAsync(int auditionId, int userId, string? comment, CancellationToken cancellationToken);

    Task<CastingResult> RemoveVoteAsync(int auditionId, int userId, CancellationToken cancellationToken);

    Task<CastingResult> SetCommentAsync(int auditionId, int userId, string? comment, CancellationToken cancellationToken);

    Task<CastingResult> DeleteCommentAsync(int auditionId, int userId, CancellationToken cancellationToken);

    Task<CastingResult> ClearVotesAsync(int characterId, int userId, CancellationToken cancellationToken);

    Task<CastingResult> SetDoneAsync(int characterId, int userId, bool done, CancellationToken cancellationToken);
}

/// <summary>
/// The voter's side of casting. Voters only ever see open rounds, their own votes, and, once they have
/// marked a character done, other voters' comments on it as bare text. Counts and identities stay with the review.
/// </summary>
public sealed class CastingVotingService(
    ICastingRoundRepository rounds,
    ICastingVoteRepository votes,
    ICastingAudioStorage storage,
    TimeProvider timeProvider) : ICastingVotingService
{
    public async Task<CastingRoundListResponse> GetOpenRoundsAsync(int userId, CancellationToken cancellationToken)
    {
        var openRounds = await rounds.GetRoundsAsync([CastingRoundStatus.Open], cancellationToken);
        var summaries = new List<CastingRoundSummaryResponse>(openRounds.Count);
        foreach (var round in openRounds)
        {
            var characters = await rounds.GetCharactersAsync(round.Id, cancellationToken);
            var done = await votes.GetDoneForRoundAsync(round.Id, cancellationToken);
            var roundVotes = await votes.GetVotesForRoundAsync(round.Id, cancellationToken);
            summaries.Add(new CastingRoundSummaryResponse(
                round.Id,
                round.Name,
                round.Description,
                round.VotingClosesAt,
                round.IsVotingOpen(Now),
                characters.Count,
                done.Count(d => d.UserId == userId),
                roundVotes.Count(v => v.UserId == userId && v.Picked)));
        }

        return new CastingRoundListResponse(summaries);
    }

    public async Task<CastingRoundDetailResponse?> GetRoundAsync(int roundId, int userId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (!IsVisible(round))
        {
            return null;
        }

        var characters = await rounds.GetCharactersAsync(roundId, cancellationToken);
        var auditions = await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken);
        var myVotes = (await votes.GetVotesForRoundAsync(roundId, cancellationToken))
            .Where(v => v.UserId == userId)
            .ToList();
        var myDone = (await votes.GetDoneForRoundAsync(roundId, cancellationToken))
            .Where(d => d.UserId == userId)
            .Select(d => d.CharacterId)
            .ToHashSet();

        return new CastingRoundDetailResponse(
            round!.Id,
            round.Name,
            round.Description,
            round.VotingClosesAt,
            round.IsVotingOpen(Now),
            characters.Select(c => new CastingCharacterSummaryResponse(
                c.Id,
                c.Name,
                c.QuestName,
                c.Direction,
                auditions.Count(a => a.CharacterId == c.Id),
                myVotes.Count(v => v.CharacterId == c.Id && v.Picked),
                myDone.Contains(c.Id))).ToList());
    }

    public async Task<CastingMyPicksResponse?> GetMyPicksAsync(int roundId, int userId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (!IsVisible(round))
        {
            return null;
        }

        var auditions = (await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken)).ToDictionary(a => a.Id);
        var picks = (await votes.GetVotesForRoundAsync(roundId, cancellationToken))
            .Where(v => v.UserId == userId && auditions.ContainsKey(v.AuditionId))
            .Select(v =>
            {
                var audition = auditions[v.AuditionId];
                return new CastingMyPickResponse(
                    audition.CharacterId,
                    audition.Id,
                    audition.Number,
                    audition.AuditioneeName,
                    storage.GetReadUrl(audition.AudioBlobPath).ToString(),
                    v.Picked,
                    v.Comment);
            })
            .ToList();

        return new CastingMyPicksResponse(picks);
    }

    public async Task<CastingAuditionListResponse?> GetAuditionsAsync(
        int characterId,
        int userId,
        CancellationToken cancellationToken)
    {
        var (round, character) = await LoadCharacterAsync(characterId, cancellationToken);
        if (!IsVisible(round) || character is null)
        {
            return null;
        }

        var auditions = await rounds.GetAuditionsAsync(characterId, cancellationToken);
        var characterVotes = await votes.GetVotesForCharacterAsync(characterId, cancellationToken);
        // Others' comments stay hidden until the voter is done, so they cannot sway the vote.
        var revealed = (await votes.GetDoneForRoundAsync(round!.Id, cancellationToken))
            .Any(d => d.CharacterId == characterId && d.UserId == userId);

        var response = auditions.Select(audition =>
        {
            var mine = characterVotes.FirstOrDefault(v => v.AuditionId == audition.Id && v.UserId == userId);
            var others = revealed
                ? characterVotes
                    .Where(v => v.AuditionId == audition.Id && v.UserId != userId && !string.IsNullOrWhiteSpace(v.Comment))
                    .Select(v => v.Comment!)
                    .ToList()
                : [];

            return new CastingAuditionResponse(
                audition.Id,
                audition.Number,
                audition.AuditioneeName,
                storage.GetReadUrl(audition.AudioBlobPath).ToString(),
                audition.DurationSeconds,
                mine?.Picked == true,
                mine?.Comment,
                others);
        }).ToList();

        return new CastingAuditionListResponse(characterId, revealed, response);
    }

    public async Task<CastingResult> VoteAsync(
        int auditionId,
        int userId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var audition = await rounds.GetAuditionAsync(auditionId, cancellationToken);
        if (audition is null)
        {
            return CastingResult.NotFound();
        }

        var check = await CheckCanChangeVotesAsync(audition.CharacterId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        var trimmed = comment?.Trim();
        await votes.UpsertVoteAsync(auditionId, userId, string.IsNullOrEmpty(trimmed) ? null : trimmed, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> RemoveVoteAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        var audition = await rounds.GetAuditionAsync(auditionId, cancellationToken);
        if (audition is null)
        {
            return CastingResult.NotFound();
        }

        var check = await CheckCanChangeVotesAsync(audition.CharacterId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await votes.UnpickAsync(auditionId, userId, cancellationToken);
        return CastingResult.Success();
    }

    /// <summary>Comments stand on their own: they can be left on auditions the voter did not pick.</summary>
    public async Task<CastingResult> SetCommentAsync(
        int auditionId,
        int userId,
        string? comment,
        CancellationToken cancellationToken)
    {
        var trimmed = comment?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return await DeleteCommentAsync(auditionId, userId, cancellationToken);
        }

        var check = await CheckCanChangeAuditionAsync(auditionId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await votes.UpsertCommentAsync(auditionId, userId, trimmed, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> DeleteCommentAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        var check = await CheckCanChangeAuditionAsync(auditionId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await votes.DeleteCommentAsync(auditionId, userId, cancellationToken);
        return CastingResult.Success();
    }

    private async Task<CastingResult> CheckCanChangeAuditionAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        var audition = await rounds.GetAuditionAsync(auditionId, cancellationToken);
        return audition is null
            ? CastingResult.NotFound()
            : await CheckCanChangeVotesAsync(audition.CharacterId, userId, cancellationToken);
    }

    public async Task<CastingResult> ClearVotesAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        var check = await CheckCanChangeVotesAsync(characterId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await votes.UnpickAllForCharacterAsync(characterId, userId, cancellationToken);
        return CastingResult.Success();
    }

    public async Task<CastingResult> SetDoneAsync(
        int characterId,
        int userId,
        bool done,
        CancellationToken cancellationToken)
    {
        var (round, character) = await LoadCharacterAsync(characterId, cancellationToken);
        if (!IsVisible(round) || character is null)
        {
            return CastingResult.NotFound();
        }

        if (!round!.IsVotingOpen(Now))
        {
            return VotingClosed();
        }

        if (done)
        {
            await votes.SetDoneAsync(characterId, userId, cancellationToken);
        }
        else
        {
            await votes.ClearDoneAsync(characterId, userId, cancellationToken);
        }

        return CastingResult.Success();
    }

    /// <summary>
    /// Votes are frozen once a character is marked done: the voter has seen the others' comments by then,
    /// so changing a pick would defeat the point of hiding them. Reopening unfreezes it.
    /// </summary>
    private async Task<CastingResult> CheckCanChangeVotesAsync(
        int characterId,
        int userId,
        CancellationToken cancellationToken)
    {
        var (round, character) = await LoadCharacterAsync(characterId, cancellationToken);
        if (!IsVisible(round) || character is null)
        {
            return CastingResult.NotFound();
        }

        if (!round!.IsVotingOpen(Now))
        {
            return VotingClosed();
        }

        var isDone = (await votes.GetDoneForRoundAsync(round.Id, cancellationToken))
            .Any(d => d.CharacterId == characterId && d.UserId == userId);
        return isDone
            ? CastingResult.Invalid("character", "You marked this character as done. Reopen it to change your votes.")
            : CastingResult.Success();
    }

    private async Task<(CastingRound? Round, CastingCharacter? Character)> LoadCharacterAsync(
        int characterId,
        CancellationToken cancellationToken)
    {
        var character = await rounds.GetCharacterAsync(characterId, cancellationToken);
        if (character is null)
        {
            return (null, null);
        }

        return (await rounds.GetRoundAsync(character.RoundId, cancellationToken), character);
    }

    /// <summary>Voters only reach open rounds; drafts and finished rounds look like they do not exist.</summary>
    private static bool IsVisible(CastingRound? round) => round is { Status: CastingRoundStatus.Open };

    private static CastingResult VotingClosed() =>
        CastingResult.Invalid("round", "Voting for this casting has closed.");

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;
}
