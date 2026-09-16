using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Content;

public sealed class NpcLookupRequest
{
    [Required]
    [StringLength(63, MinimumLength = 1)]
    public string Q { get; init; } = string.Empty;

    [Range(1, 500)]
    public int Limit { get; init; } = 100;

    /// <summary>Restricts the results to NPCs that have no stored portrait.</summary>
    public bool MissingPicture { get; init; }
}

public sealed record NpcLookupResponse(IReadOnlyList<NpcLookupResult> Results);

public sealed record NpcLookupResult(
    int NpcId,
    string Name,
    NpcLastSeenResponse? LastSeenAt);

public sealed record NpcLastSeenResponse(int X, int Y, int Z);
