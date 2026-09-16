using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Mod;

public sealed class ModBootupRequest
{
    /// <summary>
    /// SHA-256 of the player's Minecraft UUID, hashed client-side so the API never sees the UUID.
    /// </summary>
    [Required]
    [RegularExpression("^[0-9a-f]{64}$", ErrorMessage = "clientId must be a lowercase SHA-256 hex digest.")]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    [StringLength(Domain.Mod.ModVersion.MaxLength)]
    public string ModVersion { get; init; } = string.Empty;
}
