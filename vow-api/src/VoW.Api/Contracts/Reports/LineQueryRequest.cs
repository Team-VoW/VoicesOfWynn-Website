using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Reports;

public sealed class LineQueryRequest
{
    [StringLength(127)]
    public string? Npc { get; init; }

    /// <summary>
    /// Repeated query parameter. Replaces the three fixed PHP endpoints, which hard-coded a
    /// status set each and were otherwise identical.
    /// </summary>
    public string[] Statuses { get; init; } = [];

    [Range(0, int.MaxValue)]
    public int MinReports { get; init; } = 1;

    /// <summary>Lower bound on time_submitted, as yyyy-MM-dd.</summary>
    public string? Since { get; init; }

    [Range(1, 2000)]
    public int Limit { get; init; } = 500;

    [Range(0, int.MaxValue)]
    public int Offset { get; init; }
}
