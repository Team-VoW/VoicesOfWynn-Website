namespace VoW.Api.Domain.Accounts;

public sealed record AccountSearchPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<AccountSummary> Results);
