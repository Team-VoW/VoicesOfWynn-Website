namespace VoW.Api.Services.Accounts;

public sealed record CreateAccountServiceResult(
    int? UserId,
    string? TemporaryPassword,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0 && UserId is not null && TemporaryPassword is not null;

    public static CreateAccountServiceResult Success(int userId, string temporaryPassword) =>
        new(userId, temporaryPassword, new Dictionary<string, string>());

    public static CreateAccountServiceResult Invalid(string field, string message) =>
        new(null, null, new Dictionary<string, string> { [field] = message });
}
