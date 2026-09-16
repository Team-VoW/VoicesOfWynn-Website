using VoW.Api.Domain.Mod;

namespace VoW.Api.Contracts.Mod;

public sealed record ModBootupResponse(
    ModUpdateResponse Update,
    ModAudioResponse Audio,
    string? FunFact,
    IReadOnlyList<string> Broadcasts);

public sealed record ModUpdateResponse(
    ModUpdateAction Action,
    string LatestVersion,
    string DownloadUrl,
    string ChangelogUrl);

public sealed record ModAudioResponse(
    string BaseUrl,
    IReadOnlyList<string> MirrorUrls);
