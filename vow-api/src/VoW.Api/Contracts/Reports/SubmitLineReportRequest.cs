using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Reports;

public sealed class SubmitLineReportRequest
{
    /// <remarks>
    /// 319 is the width of the report.chat_message column. The PHP validator allowed 511, so
    /// anything longer was accepted and then rejected or truncated by MySQL.
    /// </remarks>
    [Required]
    [StringLength(319, MinimumLength = 1)]
    public string ChatMessage { get; init; } = string.Empty;

    [StringLength(127)]
    public string? NpcName { get; init; }

    /// <summary>Null or empty reports anonymously.</summary>
    [StringLength(16)]
    public string? PlayerName { get; init; }

    public PositionRequest? Position { get; init; }
}

public sealed record PositionRequest(int X, int Y, int Z);
