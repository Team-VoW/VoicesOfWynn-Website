using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Mod;

public sealed record FunFactResponse(
    int Id,
    string Slug,
    string Content,
    bool Active,
    DateTime CreatedAt);

public sealed record FunFactListResponse(IReadOnlyList<FunFactResponse> FunFacts);

public sealed class SaveFunFactRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "slug may contain lowercase letters, digits and underscores only.")]
    public string Slug { get; init; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;

    public bool Active { get; init; } = true;
}
