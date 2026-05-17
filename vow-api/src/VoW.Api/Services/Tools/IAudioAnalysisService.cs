namespace VoW.Api.Services.Tools;

public sealed record AudioAnalysisOutcome(
    bool Success,
    double? IntegratedLufs,
    double? LeadingSilenceSeconds,
    double? TrailingSilenceSeconds,
    string? Error);

public interface IAudioAnalysisService
{
    Task<AudioAnalysisOutcome> AnalyzeAsync(Stream wavStream, CancellationToken cancellationToken);
}
