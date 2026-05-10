namespace VoW.Api.Domain.Accounts;

public sealed record AccountSearchCriteria(
    string? Query,
    int Page,
    int PageSize);
