using VoW.Api.Domain.Casting;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Casting;

/// <summary>
/// Pulls a CCC project's unsorted submissions into a round: one character per CCC role, one audition per
/// submission. Clips already imported (matched by their CCC audio url) are skipped, so re-running an
/// import after new submissions arrive only adds the new ones.
/// </summary>
public sealed class CccImportService(
    ICastingRoundRepository rounds,
    ICccClient cccClient,
    CastingAuditionIngestService ingest,
    ILogger<CccImportService> logger)
{
    public async Task RunAsync(CccImportJob job, CancellationToken cancellationToken)
    {
        await rounds.SetImportStateAsync(job.RoundId, CastingImportStatus.Running, "Reading submissions from Casting Call Club…", cancellationToken);

        IReadOnlyList<CccSubmission> submissions;
        try
        {
            submissions = await cccClient.GetUnsortedSubmissionsAsync(job.ProjectUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is CccException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            await rounds.SetImportStateAsync(job.RoundId, CastingImportStatus.Failed, ex.Message, cancellationToken);
            return;
        }

        var added = 0;
        var skipped = 0;
        var failed = new List<string>();
        var processed = 0;
        foreach (var role in submissions.GroupBy(s => s.RoleName))
        {
            var character = await EnsureCharacterAsync(job.RoundId, role.Key, cancellationToken);
            foreach (var submission in role)
            {
                processed++;
                try
                {
                    var existing = await ingest.FindExistingAsync(character, submission.AudioUrl, cancellationToken);
                    if (existing is not null)
                    {
                        skipped++;
                        continue;
                    }

                    await using var audio = await cccClient.DownloadAudioAsync(submission.AudioUrl, cancellationToken);
                    var result = await ingest.AddAsync(
                        character,
                        new AuditionSubmission(submission.Username, null, submission.AudioUrl),
                        audio,
                        cancellationToken);
                    if (result.Created) added++; else skipped++;
                }
                catch (Exception ex) when (ex is CccException or AudioTranscodeException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Skipping CCC submission by {Username} for {Role}.", submission.Username, role.Key);
                    failed.Add($"{submission.Username} ({role.Key})");
                }

                if (processed % 5 == 0)
                {
                    await rounds.SetImportStateAsync(
                        job.RoundId,
                        CastingImportStatus.Running,
                        $"Imported {processed} of {submissions.Count} submissions…",
                        cancellationToken);
                }
            }
        }

        var summary = $"Imported {added} new, skipped {skipped} already imported, from {submissions.Count} unsorted submissions.";
        if (failed.Count > 0)
        {
            summary += $" Failed: {string.Join(", ", failed)}.";
        }

        await rounds.SetImportStateAsync(
            job.RoundId,
            failed.Count > 0 && added == 0 && submissions.Count > 0 ? CastingImportStatus.Failed : CastingImportStatus.Done,
            summary,
            cancellationToken);
    }

    private async Task<CastingCharacter> EnsureCharacterAsync(int roundId, string name, CancellationToken cancellationToken)
    {
        var trimmed = name.Length > 100 ? name[..100] : name;
        var existing = await rounds.FindCharacterByNameAsync(roundId, trimmed, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var id = await rounds.CreateCharacterAsync(roundId, new CastingCharacterDetails(trimmed, null, null), cancellationToken);
        return (id is null ? null : await rounds.GetCharacterAsync(id.Value, cancellationToken))
               ?? await rounds.FindCharacterByNameAsync(roundId, trimmed, cancellationToken)
               ?? throw new InvalidOperationException($"Could not create casting character {trimmed}.");
    }
}
