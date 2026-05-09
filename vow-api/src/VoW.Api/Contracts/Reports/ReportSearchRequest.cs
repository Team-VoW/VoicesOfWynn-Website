using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Reports;

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
