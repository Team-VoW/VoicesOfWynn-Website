using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Contracts.Contributors;

public sealed record ContributorSearchRequest(
    [Range(1, int.MaxValue)]
    int Page = 1,
    [Range(1, 100)]
    int PageSize = 24);
