using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Mod;

public sealed record BroadcastResponse(
    int Id,
    string Content,
    DateTime ActiveFrom,
    DateTime ActiveUntil,
    DateTime CreatedAt,
    int? CreatedBy);

public sealed record BroadcastListResponse(IReadOnlyList<BroadcastResponse> Broadcasts);

public sealed class SaveBroadcastRequest
{
    [Required]
    [StringLength(511, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    /// <summary>Interpreted as UTC. The ini-based loader compared against server-local time.</summary>
    [Required]
    public DateTime ActiveFrom { get; init; }

    [Required]
    public DateTime ActiveUntil { get; init; }
}
