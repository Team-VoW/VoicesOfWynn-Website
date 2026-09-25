using VoW.Api.Contracts.Casting;
using VoW.Api.Domain.Auth;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Casting;

public interface ICastingReviewService
{
    Task<CastingReviewResponse?> GetReviewAsync(int roundId, CancellationToken cancellationToken);
}

/// <summary>The only place voter names are attached to votes. Casting managers only.</summary>
public sealed class CastingReviewService(
    ICastingRoundRepository rounds,
    ICastingVoteRepository votes,
    ICastingAudioStorage storage) : ICastingReviewService
{
    public async Task<CastingReviewResponse?> GetReviewAsync(int roundId, CancellationToken cancellationToken)
    {
        var round = await rounds.GetRoundAsync(roundId, cancellationToken);
        if (round is null)
        {
            return null;
        }

        var characters = await rounds.GetCharactersAsync(roundId, cancellationToken);
        var auditions = await rounds.GetAuditionsForRoundAsync(roundId, cancellationToken);
        var roundVotes = await votes.GetVotesForRoundAsync(roundId, cancellationToken);
        var done = await votes.GetDoneForRoundAsync(roundId, cancellationToken);
        var eligible = await votes.GetUsersWithRolesAsync(CapabilityMapper.CastingVoterRoles, cancellationToken);

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
                        auditionVotes.Select(v => new CastingReviewVoteResponse(NameOf(v.UserId), v.Picked, v.Comment)).ToList());
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
