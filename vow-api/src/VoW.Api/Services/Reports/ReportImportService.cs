using System.Text.Json;
using VoW.Api.Contracts.Reports;
using VoW.Api.Repositories;

namespace VoW.Api.Services.Reports;

public sealed class ReportImportService(
    IReportRepository reportRepository,
    ILogger<ReportImportService> logger) : IReportImportService
{
    /// <summary>Matches the report.chat_message column width.</summary>
    private const int ChatMessageMaxLength = 319;

    private const int ImportChunkSize = 500;

    private static readonly JsonSerializerOptions SoundsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public async Task<ReportImportServiceResult> ImportVoicedLinesAsync(
        Stream soundsJson,
        CancellationToken cancellationToken)
    {
        if (await IsNotJsonArrayAsync(soundsJson, cancellationToken))
        {
            return ReportImportServiceResult.Failure(
                "file",
                "Expected a JSON array of sound entries, like the sounds.json shipped with the mod.");
        }

        List<SoundEntry>? entries;
        try
        {
            entries = await JsonSerializer.DeserializeAsync<List<SoundEntry>>(
                soundsJson,
                SoundsJsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Rejected a sounds.json upload that could not be parsed.");
            return ReportImportServiceResult.Failure(
                "file",
                $"The uploaded file is not a valid sounds.json document: {exception.Message}");
        }

        if (entries is null)
        {
            return ReportImportServiceResult.Failure("file", "Expected a JSON array of sound entries.");
        }

        if (entries.Count == 0)
        {
            return ReportImportServiceResult.Failure("file", "The uploaded file contains no entries.");
        }

        var lines = new List<string>(entries.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var noAudioFile = 0;
        var blankLine = 0;
        var tooLong = 0;
        var duplicateInFile = 0;

        foreach (var entry in entries)
        {
            // A line without an audio file is not voiced, so it is neither marked nor imported.
            if (string.IsNullOrWhiteSpace(entry.File))
            {
                noAudioFile++;
                continue;
            }

            var line = entry.Line?.Trim();
            if (string.IsNullOrEmpty(line))
            {
                blankLine++;
                continue;
            }

            if (line.Length > ChatMessageMaxLength)
            {
                tooLong++;
                continue;
            }

            if (!seen.Add(line))
            {
                duplicateInFile++;
                continue;
            }

            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return ReportImportServiceResult.Failure(
                "file",
                "No importable lines were found. Every entry was missing either an audio file or a line.");
        }

        var counts = await reportRepository.MarkLinesAsVoicedAsync(lines, ImportChunkSize, cancellationToken);

        var skipped = new ImportVoicedLinesSkipped(
            noAudioFile,
            blankLine,
            tooLong,
            duplicateInFile,
            noAudioFile + blankLine + tooLong + duplicateInFile);

        logger.LogInformation(
            "Imported voiced lines from sounds.json: {Total} entries, {Unique} unique lines, {Present} already present, {Inserted} inserted, {Skipped} skipped.",
            entries.Count,
            lines.Count,
            counts.AlreadyPresent,
            counts.Inserted,
            skipped.Total);

        return ReportImportServiceResult.Success(new ImportVoicedLinesResponse(
            entries.Count,
            lines.Count,
            counts.AlreadyPresent,
            counts.Inserted,
            skipped));
    }

    /// <summary>
    /// Peeks at the first meaningful character so a wrong root type gets a readable message instead of
    /// the serializer's "could not be converted to List&lt;SoundEntry&gt;", which leaks an internal type
    /// name into the UI. Returns false when the stream cannot be rewound, leaving the check to the
    /// serializer.
    /// </summary>
    private static async Task<bool> IsNotJsonArrayAsync(Stream soundsJson, CancellationToken cancellationToken)
    {
        if (!soundsJson.CanSeek)
        {
            return false;
        }

        var buffer = new byte[1];
        while (await soundsJson.ReadAsync(buffer, cancellationToken) == 1)
        {
            if (char.IsWhiteSpace((char)buffer[0]))
            {
                continue;
            }

            soundsJson.Position = 0;
            return buffer[0] != (byte)'[';
        }

        // Empty or whitespace-only: let the serializer produce the parse error.
        soundsJson.Position = 0;
        return false;
    }

    /// <summary>
    /// Only the two fields the import needs. The remaining sounds.json fields (onPlayer, fallOff,
    /// pos, npc, reverb, stopSounds) are deliberately not modelled: their types are inconsistent in
    /// the real file - stopSounds appears both as a boolean and as the string "false" - and
    /// System.Text.Json ignores unmapped members, so leaving them out keeps parsing tolerant.
    /// Do not add them.
    /// </summary>
    private sealed class SoundEntry
    {
        public string? Line { get; set; }

        public string? File { get; set; }
    }
}
