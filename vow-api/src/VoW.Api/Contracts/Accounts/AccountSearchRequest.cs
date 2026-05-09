namespace VoW.Api.Contracts.Accounts;

public sealed record AccountSearchRequest(
    string? Query,
    int Page = 1,
    int PageSize = 25);
