using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Models;

public static class ReportStatus
{
    public static readonly string[] Values =
    [
        "unprocessed",
        "forwarded",
        "rejected",
        "accepted",
        "fixed"
    ];

    public static readonly string DisplayList = string.Join(", ", Values);

    private static readonly HashSet<string> ValidValues = new(Values, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string status) => ValidValues.Contains(status);
}

public sealed class ReportSearchRequest
{
    [StringLength(127)]
    public string? Npc { get; init; }

    [StringLength(319)]
    public string? Content { get; init; }

    public string? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;
}

public sealed record ReportSearchResponse(
    int Total,
    int Page,
    IReadOnlyList<ReportSearchResult> Results);

public sealed record ReportSearchResult(
    int ReportId,
    string? NpcName,
    string ChatMessage,
    string Status,
    int ReportedTimes,
    DateTime TimeSubmitted);
