namespace VoW.Api.Contracts.Tools;

public sealed record AudioAnalysisItem(
    string FileName,
    bool Success,
    double? IntegratedLufs,
    double? MaxTruePeakDbtp,
    double? LeadingSilenceSeconds,
    double? TrailingSilenceSeconds,
    string? ChannelMode,
    string? Error);

public sealed record AudioAnalysisBatchResponse(IReadOnlyList<AudioAnalysisItem> Results);
