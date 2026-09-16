namespace VoW.Api.Domain.Mod;

/// <summary>
/// The single row of mod bootup configuration. Replaces version.ini from the PHP app, where the
/// wire format of /api/version/check was literally whatever keys the ini file happened to have.
/// </summary>
public sealed record ModRelease(
    string LatestVersion,
    string UpdateNotificationVersion,
    string KillSwitchVersion,
    string DownloadUrl,
    string ChangelogUrl,
    string AudioBaseUrl,
    IReadOnlyList<string> AudioMirrorUrls,
    DateTime UpdatedAt,
    int? UpdatedBy);
