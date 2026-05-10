namespace VoW.Api.Contracts.Accounts;

public sealed record AccountSearchResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<AccountSearchResult> Results);
