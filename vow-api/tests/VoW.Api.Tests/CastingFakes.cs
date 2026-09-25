using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;
using VoW.Api.Services.Casting;
using VoW.Api.Services.Storage;

namespace VoW.Api.Tests;

internal sealed class MemoryCastingRounds : ICastingRoundRepository
{
    public List<CastingRound> Rounds { get; } = [];
    public List<CastingCharacter> Characters { get; } = [];
    public List<CastingAudition> Auditions { get; } = [];

    public int AddRound(CastingRoundStatus status, DateTime? closesAt = null)
    {
        var id = Rounds.Count + 1;
        Rounds.Add(new CastingRound(id, $"Round {id}", null, status, CastingSource.Manual, null, closesAt,
            CastingImportStatus.Idle, null, null, DateTime.UtcNow, DateTime.UtcNow));
        return id;
    }

    public int AddCharacter(int roundId, string name = "Theorick")
    {
        var id = Characters.Count + 1;
        Characters.Add(new CastingCharacter(id, roundId, name, "Detlas", "Weary knight", id, null));
        return id;
    }

    public int AddAudition(int characterId, string name = "Auditionee", string? sourceRef = null)
    {
        var id = Auditions.Count + 1;
        var number = Auditions.Count(a => a.CharacterId == characterId) + 1;
        Auditions.Add(new CastingAudition(id, characterId, number, name, null, sourceRef, $"blob/{id}.mp3", 12, DateTime.UtcNow));
        return id;
    }

    public Task<IReadOnlyList<CastingRound>> GetRoundsAsync(IReadOnlyCollection<CastingRoundStatus> statuses, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingRound>>(Rounds.Where(r => statuses.Contains(r.Status)).ToList());

    public Task<CastingRound?> GetRoundAsync(int roundId, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.FirstOrDefault(r => r.Id == roundId));

    public Task<CastingRound?> FindActiveRoundBySourceAsync(CastingSource source, string sourceRef, CancellationToken cancellationToken) =>
        Task.FromResult(Rounds.LastOrDefault(r => r.Source == source && r.SourceRef == sourceRef && r.Status != CastingRoundStatus.Archived));

    public Task<int> CreateRoundAsync(NewCastingRound round, CancellationToken cancellationToken)
    {
        var id = Rounds.Count + 1;
        Rounds.Add(new CastingRound(id, round.Details.Name, round.Details.Description, CastingRoundStatus.Draft, round.Source,
            round.SourceRef, round.Details.VotingClosesAt, CastingImportStatus.Idle, null,
            round.CreatedBy, DateTime.UtcNow, DateTime.UtcNow));
        return Task.FromResult(id);
    }

    public Task UpdateRoundAsync(int roundId, CastingRoundDetails details, CancellationToken cancellationToken)
    {
        Replace(roundId, r => r with
        {
            Name = details.Name,
            Description = details.Description,
            VotingClosesAt = details.VotingClosesAt
        });
        return Task.CompletedTask;
    }

    public Task SetRoundStatusAsync(int roundId, CastingRoundStatus status, CancellationToken cancellationToken)
    {
        Replace(roundId, r => r with { Status = status });
        return Task.CompletedTask;
    }

    public Task SetRoundSourceAsync(int roundId, CastingSource source, string? sourceRef, CancellationToken cancellationToken)
    {
        Replace(roundId, r => r with { Source = source, SourceRef = sourceRef });
        return Task.CompletedTask;
    }

    public Task SetImportStateAsync(int roundId, CastingImportStatus status, string? message, CancellationToken cancellationToken)
    {
        Replace(roundId, r => r with { ImportStatus = status, ImportMessage = message });
        return Task.CompletedTask;
    }

    public Task DeleteRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var characterIds = Characters.Where(c => c.RoundId == roundId).Select(c => c.Id).ToHashSet();
        Auditions.RemoveAll(a => characterIds.Contains(a.CharacterId));
        Characters.RemoveAll(c => c.RoundId == roundId);
        Rounds.RemoveAll(r => r.Id == roundId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CastingCharacter>> GetCharactersAsync(int roundId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingCharacter>>(Characters.Where(c => c.RoundId == roundId).OrderBy(c => c.SortOrder).ToList());

    public Task<CastingCharacter?> GetCharacterAsync(int characterId, CancellationToken cancellationToken) =>
        Task.FromResult(Characters.FirstOrDefault(c => c.Id == characterId));

    public Task<CastingCharacter?> FindCharacterByNameAsync(int roundId, string name, CancellationToken cancellationToken) =>
        Task.FromResult(Characters.FirstOrDefault(c => c.RoundId == roundId && c.Name == name));

    public Task<int?> CreateCharacterAsync(int roundId, CastingCharacterDetails details, CancellationToken cancellationToken)
    {
        if (Characters.Any(c => c.RoundId == roundId && c.Name == details.Name))
        {
            return Task.FromResult<int?>(null);
        }

        var id = Characters.Count == 0 ? 1 : Characters.Max(c => c.Id) + 1;
        Characters.Add(new CastingCharacter(id, roundId, details.Name, details.QuestName, details.Direction, id, null));
        return Task.FromResult<int?>(id);
    }

    public Task<bool> UpdateCharacterAsync(int characterId, CastingCharacterDetails details, CancellationToken cancellationToken)
    {
        var index = Characters.FindIndex(c => c.Id == characterId);
        Characters[index] = Characters[index] with { Name = details.Name, QuestName = details.QuestName, Direction = details.Direction };
        return Task.FromResult(true);
    }

    public Task DeleteCharacterAsync(int characterId, CancellationToken cancellationToken)
    {
        Auditions.RemoveAll(a => a.CharacterId == characterId);
        Characters.RemoveAll(c => c.Id == characterId);
        return Task.CompletedTask;
    }

    public Task SetWinnerAsync(int characterId, int? auditionId, CancellationToken cancellationToken)
    {
        var index = Characters.FindIndex(c => c.Id == characterId);
        Characters[index] = Characters[index] with { WinnerAuditionId = auditionId };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CastingAudition>> GetAuditionsForRoundAsync(int roundId, CancellationToken cancellationToken)
    {
        var characterIds = Characters.Where(c => c.RoundId == roundId).Select(c => c.Id).ToHashSet();
        return Task.FromResult<IReadOnlyList<CastingAudition>>(Auditions.Where(a => characterIds.Contains(a.CharacterId)).ToList());
    }

    public Task<IReadOnlyList<CastingAudition>> GetAuditionsAsync(int characterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingAudition>>(Auditions.Where(a => a.CharacterId == characterId).ToList());

    public Task<CastingAudition?> GetAuditionAsync(int auditionId, CancellationToken cancellationToken) =>
        Task.FromResult(Auditions.FirstOrDefault(a => a.Id == auditionId));

    public Task<CastingAudition?> FindAuditionBySourceAsync(int characterId, string sourceRef, CancellationToken cancellationToken) =>
        Task.FromResult(Auditions.FirstOrDefault(a => a.CharacterId == characterId && a.SourceRef == sourceRef));

    public Task<int?> CreateAuditionAsync(NewCastingAudition audition, CancellationToken cancellationToken)
    {
        if (audition.SourceRef is not null && Auditions.Any(a => a.CharacterId == audition.CharacterId && a.SourceRef == audition.SourceRef))
        {
            return Task.FromResult<int?>(null);
        }

        var id = Auditions.Count == 0 ? 1 : Auditions.Max(a => a.Id) + 1;
        var number = Auditions.Count(a => a.CharacterId == audition.CharacterId) + 1;
        Auditions.Add(new CastingAudition(id, audition.CharacterId, number, audition.AuditioneeName, audition.AuditioneeUserId,
            audition.SourceRef, audition.AudioBlobPath, audition.DurationSeconds, DateTime.UtcNow));
        return Task.FromResult<int?>(id);
    }

    public Task DeleteAuditionAsync(int auditionId, CancellationToken cancellationToken)
    {
        Auditions.RemoveAll(a => a.Id == auditionId);
        return Task.CompletedTask;
    }

    private void Replace(int roundId, Func<CastingRound, CastingRound> change)
    {
        var index = Rounds.FindIndex(r => r.Id == roundId);
        Rounds[index] = change(Rounds[index]);
    }
}

internal sealed class MemoryCastingVotes(MemoryCastingRounds rounds) : ICastingVoteRepository
{
    public List<(int AuditionId, int UserId, bool Picked, string? Comment)> Votes { get; } = [];
    public HashSet<(int CharacterId, int UserId)> Done { get; } = [];
    public List<(CastingVoter Voter, DiscordRoleId Role)> Users { get; } = [];

    private int CharacterOf(int auditionId) => rounds.Auditions.First(a => a.Id == auditionId).CharacterId;

    private int RoundOf(int characterId) => rounds.Characters.First(c => c.Id == characterId).RoundId;

    public Task<IReadOnlyList<CastingVote>> GetVotesForRoundAsync(int roundId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingVote>>(Votes
            .Select(v => new CastingVote(v.AuditionId, CharacterOf(v.AuditionId), v.UserId, v.Picked, v.Comment))
            .Where(v => RoundOf(v.CharacterId) == roundId)
            .ToList());

    public Task<IReadOnlyList<CastingVote>> GetVotesForCharacterAsync(int characterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingVote>>(Votes
            .Select(v => new CastingVote(v.AuditionId, CharacterOf(v.AuditionId), v.UserId, v.Picked, v.Comment))
            .Where(v => v.CharacterId == characterId)
            .ToList());

    public Task UpsertVoteAsync(int auditionId, int userId, string? comment, CancellationToken cancellationToken)
    {
        Votes.RemoveAll(v => v.AuditionId == auditionId && v.UserId == userId);
        Votes.Add((auditionId, userId, true, comment));
        return Task.CompletedTask;
    }

    public Task UpsertCommentAsync(int auditionId, int userId, string comment, CancellationToken cancellationToken)
    {
        var index = Votes.FindIndex(v => v.AuditionId == auditionId && v.UserId == userId);
        if (index < 0) Votes.Add((auditionId, userId, false, comment));
        else Votes[index] = Votes[index] with { Comment = comment };
        return Task.CompletedTask;
    }

    public Task UnpickAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        Change(v => v.AuditionId == auditionId && v.UserId == userId, v => v with { Picked = false });
        return Task.CompletedTask;
    }

    public Task UnpickAllForCharacterAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        Change(v => v.UserId == userId && CharacterOf(v.AuditionId) == characterId, v => v with { Picked = false });
        return Task.CompletedTask;
    }

    public Task DeleteCommentAsync(int auditionId, int userId, CancellationToken cancellationToken)
    {
        Change(v => v.AuditionId == auditionId && v.UserId == userId, v => v with { Comment = null });
        return Task.CompletedTask;
    }

    /// <summary>Applies the change, then drops rows that are neither picked nor commented, like the SQL does.</summary>
    private void Change(
        Func<(int AuditionId, int UserId, bool Picked, string? Comment), bool> match,
        Func<(int AuditionId, int UserId, bool Picked, string? Comment), (int AuditionId, int UserId, bool Picked, string? Comment)> change)
    {
        for (var i = 0; i < Votes.Count; i++)
        {
            if (match(Votes[i])) Votes[i] = change(Votes[i]);
        }

        Votes.RemoveAll(v => !v.Picked && v.Comment is null);
    }

    public Task<IReadOnlyList<CastingDone>> GetDoneForRoundAsync(int roundId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingDone>>(Done
            .Where(d => RoundOf(d.CharacterId) == roundId)
            .Select(d => new CastingDone(d.CharacterId, d.UserId))
            .ToList());

    public Task SetDoneAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        Done.Add((characterId, userId));
        return Task.CompletedTask;
    }

    public Task ClearDoneAsync(int characterId, int userId, CancellationToken cancellationToken)
    {
        Done.Remove((characterId, userId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CastingVoter>> GetUsersWithRolesAsync(IReadOnlyCollection<DiscordRoleId> roles, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingVoter>>(Users.Where(u => roles.Contains(u.Role)).Select(u => u.Voter).DistinctBy(v => v.UserId).ToList());

    public Task<IReadOnlyList<CastingVoter>> GetUsersAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CastingVoter>>(Users.Select(u => u.Voter).Where(v => userIds.Contains(v.UserId)).DistinctBy(v => v.UserId).ToList());
}

internal sealed class MemoryCastingAudio : ICastingAudioStorage
{
    public HashSet<string> Blobs { get; } = [];

    public Task UploadAsync(string blobPath, Stream content, CancellationToken cancellationToken)
    {
        Blobs.Add(blobPath);
        return Task.CompletedTask;
    }

    public Uri GetReadUrl(string blobPath) => new($"https://blob.test/{blobPath}?sig=x");

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken)
    {
        Blobs.Remove(blobPath);
        return Task.CompletedTask;
    }
}

internal sealed class PassThroughTranscoder : IAudioTranscoder
{
    public Task<TranscodedAudio> ToMp3Async(Stream input, CancellationToken cancellationToken)
    {
        var path = Path.GetTempFileName();
        using (var file = File.Create(path))
        {
            input.CopyTo(file);
        }

        return Task.FromResult(new TranscodedAudio(path, 3));
    }
}

internal sealed class RecordingImportQueue : ICastingImportQueue
{
    public List<CccImportJob> Jobs { get; } = [];

    public bool TryEnqueue(CccImportJob job)
    {
        Jobs.Add(job);
        return true;
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
