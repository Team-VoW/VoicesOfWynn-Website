namespace VoW.Api.Contracts.Mod;

public sealed record ModReleaseResponse(
    string LatestVersion,
    string UpdateNotificationVersion,
    string KillSwitchVersion,
    string DownloadUrl,
    string ChangelogUrl,
    string AudioBaseUrl,
    IReadOnlyList<string> AudioMirrorUrls,
    DateTime UpdatedAt,
    int? UpdatedBy);
