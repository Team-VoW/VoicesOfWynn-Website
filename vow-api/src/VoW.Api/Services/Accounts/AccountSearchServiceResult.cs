using VoW.Api.Contracts.Accounts;

namespace VoW.Api.Services.Accounts;

public sealed record AccountSearchServiceResult(
    AccountSearchResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static AccountSearchServiceResult Success(AccountSearchResponse response) =>
        new(response, new Dictionary<string, string>());

    public static AccountSearchServiceResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
