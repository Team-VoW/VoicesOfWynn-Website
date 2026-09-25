using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;
using VoW.Api.Services.Storage;

namespace VoW.Api.Services.Casting;

public sealed record AuditionSubmission(
    string AuditioneeName,
    int? AuditioneeUserId,
    string? SourceRef);

public sealed record IngestedAudition(int AuditionId, bool Created);

/// <summary>
/// The single path an audition takes into a round, whether uploaded by a manager, pushed by the bot or
/// imported from Casting Call Club: skip if the source was already imported, convert to MP3, store, record.
/// </summary>
public sealed class CastingAuditionIngestService(
    ICastingRoundRepository rounds,
    IAudioTranscoder transcoder,
    ICastingAudioStorage storage)
{
    public const int MaxAuditioneeNameLength = 100;

    public async Task<IngestedAudition?> FindExistingAsync(
        CastingCharacter character,
        string? sourceRef,
        CancellationToken cancellationToken)
    {
        if (sourceRef is null)
        {
            return null;
        }

        var existing = await rounds.FindAuditionBySourceAsync(character.Id, sourceRef, cancellationToken);
        return existing is null ? null : new IngestedAudition(existing.Id, false);
    }

    /// <exception cref="AudioTranscodeException">The upload is not audio ffmpeg can read.</exception>
    public async Task<IngestedAudition> AddAsync(
        CastingCharacter character,
        AuditionSubmission submission,
        Stream audio,
        CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(character, submission.SourceRef, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        using var mp3 = await transcoder.ToMp3Async(audio, cancellationToken);
        var blobPath = $"{character.RoundId}/{character.Id}/{Guid.NewGuid():N}.mp3";
        await using (var content = mp3.OpenRead())
        {
            await storage.UploadAsync(blobPath, content, cancellationToken);
        }

        var name = submission.AuditioneeName.Trim();
        var auditionId = await rounds.CreateAuditionAsync(new NewCastingAudition(
            character.Id,
            name.Length > MaxAuditioneeNameLength ? name[..MaxAuditioneeNameLength] : name,
            submission.AuditioneeUserId,
            submission.SourceRef,
            blobPath,
            mp3.DurationSeconds), cancellationToken);

        if (auditionId is not null)
        {
            return new IngestedAudition(auditionId.Value, true);
        }

        // Lost a race with a concurrent import of the same clip: keep theirs, drop our copy.
        await storage.DeleteAsync(blobPath, cancellationToken);
        return await FindExistingAsync(character, submission.SourceRef, cancellationToken)
               ?? throw new InvalidOperationException("Audition insert collided but no existing row was found.");
    }

    public async Task DeleteAsync(CastingAudition audition, CancellationToken cancellationToken)
    {
        await rounds.DeleteAuditionAsync(audition.Id, cancellationToken);
        await storage.DeleteAsync(audition.AudioBlobPath, cancellationToken);
    }
}
