using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Casting;

public interface ICastingReviewService
{
    Task<CastingReviewResponse?> GetReviewAsync(int roundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CastingResultRoundResponse>> GetFinishedRoundsAsync(CancellationToken cancellationToken);

    Task<CastingReviewResponse?> GetFinishedReviewAsync(int roundId, CancellationToken cancellationToken);
}

public sealed record CastingResultRoundResponse(int Id, string Name, CastingRoundStatus Status);

/// <summary>Named votes are available to managers at any time and to voters after voting ends.</summary>
public sealed class CastingReviewService(
    ICastingRoundRepository rounds,
    ICastingVoteRepository votes,
    ICastingAudioStorage storage,
    TimeProvider timeProvider) : ICastingReviewService
{
    public async Task<IReadOnlyList<CastingResultRoundResponse>> GetFinishedRoundsAsync(CancellationToken cancellationToken)
    {
        var candidates = await rounds.GetRoundsAsync(
            [CastingRoundStatus.Open, CastingRoundStatus.Closed, CastingRoundStatus.Archived], cancellationToken);
        return candidates.Where(IsFinished)
            .Select(round => new CastingResultRoundResponse(round.Id, round.Name, round.Status))
            .ToList();
    }

    public async Task<CastingReviewResponse?> GetFinishedReviewAsync(int roundId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        return round is not null && IsFinished(round)
            ? await BuildReviewAsync(round, cancellationToken)
            : null;
    }

    private bool IsFinished(CastingRound round) =>
        round.Status != CastingRoundStatus.Draft && !round.IsVotingOpen(timeProvider.GetUtcNow().UtcDateTime);

    public async Task<CastingReviewResponse?> GetReviewAsync(int roundId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        return round is null ? null : await BuildReviewAsync(round, cancellationToken);
    }

    private async Task<CastingReviewResponse> BuildReviewAsync(CastingRound round, CancellationToken cancellationToken)
    {
        var characters = await rounds.GetCharactersAsync(round.Id, cancellationToken);
        var auditions = await rounds.GetAuditionsForRoundAsync(round.Id, cancellationToken);
        var roundVotes = await votes.GetVotesForRoundAsync(round.Id, cancellationToken);
        var done = await votes.GetDoneForRoundAsync(round.Id, cancellationToken);
        var eligible = await votes.GetUsersWithRolesAsync(CapabilityMapper.CastingVoterRoles, cancellationToken);
        var trialVoiceManagerIds = (await votes.GetUsersWithRolesAsync([DiscordRoleId.TrialVoiceManager], cancellationToken))
            .Select(v => v.UserId).ToHashSet();

        // Voters who have since lost their role still show up by name on the votes they cast.
        var names = eligible.ToDictionary(v => v.UserId, v => v.DisplayName);
        var unknownIds = roundVotes.Select(v => v.UserId)
            .Concat(done.Select(d => d.UserId))
            .Where(id => !names.ContainsKey(id))
            .Distinct()
            .ToList();
        foreach (var user in await votes.GetUsersAsync(unknownIds, cancellationToken))
        {
            names[user.UserId] = user.DisplayName;
        }

        string NameOf(int userId) => names.TryGetValue(userId, out var name) ? name : $"User #{userId}";

        var response = characters.Select(character =>
        {
            var characterVotes = roundVotes.Where(v => v.CharacterId == character.Id).ToList();
            var doneIds = done.Where(d => d.CharacterId == character.Id).Select(d => d.UserId).ToHashSet();
            var voterIds = characterVotes.Where(v => v.Picked).Select(v => v.UserId).ToHashSet();

            var ranked = auditions
                .Where(a => a.CharacterId == character.Id)
                .Select(a =>
                {
                    // Picks first, then comments left without a vote.
                    var auditionVotes = characterVotes
                        .Where(v => v.AuditionId == a.Id)
                        .OrderByDescending(v => v.Picked)
                        .ToList();
                    return new CastingReviewAuditionResponse(
                        a.Id,
                        a.Number,
                        a.AuditioneeName,
                        storage.GetReadUrl(a.AudioBlobPath).ToString(),
                        a.DurationSeconds,
                        auditionVotes.Count(v => v.Picked),
                        auditionVotes.Select(v => new CastingReviewVoteResponse(
                            NameOf(v.UserId), v.Picked, v.Comment, trialVoiceManagerIds.Contains(v.UserId))).ToList());
                })
                .OrderByDescending(a => a.VoteCount)
                .ThenBy(a => a.Number)
                .ToList();

            return new CastingReviewCharacterResponse(
                character.Id,
                character.Name,
                character.QuestName,
                ranked.Count,
                characterVotes.Count(v => v.Picked),
                character.WinnerAuditionId,
                doneIds.Select(NameOf).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                doneIds.Where(id => !voterIds.Contains(id)).Select(NameOf).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                eligible.Where(v => !doneIds.Contains(v.UserId)).Select(v => v.DisplayName).ToList(),
                ranked);
        }).ToList();

        return new CastingReviewResponse(round.Id, round.Name, round.Status, eligible.Count, response);
    }
}
