using Microsoft.AspNetCore.Mvc;
using VoW.Api.Contracts.Tools;
using VoW.Api.Domain.Auth;
using VoW.Api.Services.Tools;

namespace VoW.Api.Controllers.Admin;

[ApiController]
[Route("admin/tools")]
public sealed class ToolsController(IAudioAnalysisService audioAnalysisService) : ControllerBase
{
    private const long AudioAnalysisMaxSizeBytes = 100L * 1024 * 1024;

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
            if (file is null || file.Length == 0)
            {
                results.Add(Error(file?.FileName ?? "(unknown)", "File is empty."));
                continue;
            }

            if (!IsWavFile(file))
            {
                results.Add(Error(file.FileName, "Only .wav files are accepted."));
                continue;
            }

            await using var stream = file.OpenReadStream();
            var outcome = await audioAnalysisService.AnalyzeAsync(stream, cancellationToken);
            results.Add(new AudioAnalysisItem(
                FileName: file.FileName,
                Success: outcome.Success,
                IntegratedLufs: outcome.IntegratedLufs,
                MaxTruePeakDbtp: outcome.MaxTruePeakDbtp,
                LeadingSilenceSeconds: outcome.LeadingSilenceSeconds,
                TrailingSilenceSeconds: outcome.TrailingSilenceSeconds,
                ChannelMode: outcome.ChannelMode,
                Error: outcome.Error));
        }

        return Ok(new AudioAnalysisBatchResponse(results));
    }

    private static AudioAnalysisItem Error(string fileName, string message) =>
        new(
            fileName,
            Success: false,
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
}
