using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Casting;

public sealed class BotEnsureCastingRoundRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string QuestName { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public IReadOnlyList<string> Characters { get; init; } = [];
}

public sealed record BotCastingRoundResponse(int RoundId, bool Created, string AdminUrl);

public sealed class BotUploadAuditionRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CharacterName { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AuditioneeName { get; init; } = string.Empty;

    [StringLength(32)]
    public string? DiscordUserId { get; init; }

    /// <summary>The audition thread. Re-sending the same thread for a character is a no-op.</summary>
    [Required]
    [StringLength(32, MinimumLength = 1)]
    public string DiscordThreadId { get; init; } = string.Empty;

    [Required]
    public IFormFile? File { get; init; }
}

public sealed record BotAuditionResponse(int AuditionId, bool Created);
