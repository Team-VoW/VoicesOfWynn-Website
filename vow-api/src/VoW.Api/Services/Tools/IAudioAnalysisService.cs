namespace VoW.Api.Services.Tools;

public sealed record AudioAnalysisOutcome(
    bool Success,
    double? IntegratedLufs,
    double? MaxTruePeakDbtp,
    double? LeadingSilenceSeconds,
    double? TrailingSilenceSeconds,
    string? ChannelMode,
    string? Error);

public interface IAudioAnalysisService
{
    Task<AudioAnalysisOutcome> AnalyzeAsync(Stream wavStream, CancellationToken cancellationToken);
}
