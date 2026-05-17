using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using VoW.Api.Contracts.Tools;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Tools;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[Route("admin/tools")]
public sealed class ToolsController(IAudioAnalysisService audioAnalysisService) : ControllerBase
{
    private const long AudioAnalysisMaxSizeBytes = 100L * 1024 * 1024;
    private static readonly Regex AudioFileNamePattern = new(
        "^[a-z0-9]+-[a-z0-9]+-[1-9][0-9]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HttpPost("audio-analysis")]
    [RequireCapability(Capability.ToolsAudioAnalysis)]
    [RequestSizeLimit(AudioAnalysisMaxSizeBytes)]
    public async Task<ActionResult<AudioAnalysisBatchResponse>> AnalyzeAudio(
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            ModelState.AddModelError(nameof(files), "At least one audio file is required.");
            return ValidationProblem(ModelState);
        }

        var results = new List<AudioAnalysisItem>(files.Count);
        foreach (var file in files)
        {
            var fileName = file?.FileName ?? "(unknown)";
            var fileNameError = ValidateAudioFileName(fileName);

            if (file is null || file.Length == 0)
            {
                results.Add(Error(fileName, fileNameError, "File is empty."));
                continue;
            }

            if (!IsWavFile(file))
            {
                results.Add(Error(fileName, fileNameError, "Only .wav files are accepted."));
                continue;
            }

            await using var stream = file.OpenReadStream();
            var outcome = await audioAnalysisService.AnalyzeAsync(stream, cancellationToken);
            results.Add(new AudioAnalysisItem(
                FileName: fileName,
                Success: outcome.Success,
                FileNameValid: fileNameError is null,
                FileNameError: fileNameError,
                IntegratedLufs: outcome.IntegratedLufs,
                MaxTruePeakDbtp: outcome.MaxTruePeakDbtp,
                LeadingSilenceSeconds: outcome.LeadingSilenceSeconds,
                TrailingSilenceSeconds: outcome.TrailingSilenceSeconds,
                ChannelMode: outcome.ChannelMode,
                Error: outcome.Error));
        }

        return Ok(new AudioAnalysisBatchResponse(results));
    }

    private static AudioAnalysisItem Error(string fileName, string? fileNameError, string message) =>
        new(
            fileName,
            Success: false,
            FileNameValid: fileNameError is null,
            FileNameError: fileNameError,
            IntegratedLufs: null,
            MaxTruePeakDbtp: null,
            LeadingSilenceSeconds: null,
            TrailingSilenceSeconds: null,
            ChannelMode: null,
            Error: message);

    private static bool IsWavFile(IFormFile file)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var contentType = file.ContentType?.ToLowerInvariant();
        return contentType is null
            || contentType == "audio/wav"
            || contentType == "audio/wave"
            || contentType == "audio/x-wav"
            || contentType == "audio/vnd.wave"
            || contentType == "application/octet-stream";
    }

    private static string? ValidateAudioFileName(string fileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (AudioFileNamePattern.IsMatch(baseName))
        {
            return null;
        }

        return "Filename must be questname-npcname-number.wav, using only lowercase letters, numbers, exactly two hyphens, and no leading zeros in the number.";
    }
}
