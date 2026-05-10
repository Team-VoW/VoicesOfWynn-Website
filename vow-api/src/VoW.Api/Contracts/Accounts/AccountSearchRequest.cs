using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Accounts;

public sealed record AccountSearchRequest(
    string? Query,
    [Range(1, int.MaxValue)]
    int Page = 1,
    [Range(1, 100)]
    int PageSize = 25);
