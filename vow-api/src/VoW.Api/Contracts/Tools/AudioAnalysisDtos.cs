namespace VoW.Api.Contracts.Tools;

public sealed record AudioAnalysisItem(
    string FileName,
    bool Success,
    double? IntegratedLufs,
    double? LeadingSilenceSeconds,
    double? TrailingSilenceSeconds,
    string? Error);

public sealed record AudioAnalysisBatchResponse(IReadOnlyList<AudioAnalysisItem> Results);
