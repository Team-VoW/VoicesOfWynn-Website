using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Reports;

public sealed class SetLineStatusRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(2000)]
    public IReadOnlyList<string> ChatMessages { get; init; } = [];

    [Required]
    public string Status { get; init; } = string.Empty;
}

public sealed class DeleteLinesRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(2000)]
    public IReadOnlyList<string> ChatMessages { get; init; } = [];
}

public sealed record SetLineStatusResponse(int Updated, int Inserted);

public sealed record DeleteLinesResponse(int Deleted);
