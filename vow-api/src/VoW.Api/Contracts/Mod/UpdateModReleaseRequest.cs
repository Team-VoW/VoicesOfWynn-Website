using System.ComponentModel.DataAnnotations;
using VoW.Api.Domain.Mod;

namespace VoW.Api.Contracts.Mod;

public sealed class UpdateModReleaseRequest
{
    [Required]
    [StringLength(ModVersion.MaxLength)]
    public string LatestVersion { get; init; } = string.Empty;

    [Required]
    [StringLength(ModVersion.MaxLength)]
    public string UpdateNotificationVersion { get; init; } = string.Empty;

    [Required]
    [StringLength(ModVersion.MaxLength)]
    public string KillSwitchVersion { get; init; } = string.Empty;

    [Required]
    [StringLength(511)]
    public string DownloadUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(511)]
    public string ChangelogUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(511)]
    public string AudioBaseUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> AudioMirrorUrls { get; init; } = [];
}
