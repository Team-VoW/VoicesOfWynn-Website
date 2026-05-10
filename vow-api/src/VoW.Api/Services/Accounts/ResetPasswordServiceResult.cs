namespace VoW.Api.Services.Accounts;

public sealed record ResetPasswordServiceResult(
    string? TemporaryPassword,
    IReadOnlyDictionary<string, string> Errors,
    bool Found = true)
{
    public bool Succeeded => Found && Errors.Count == 0 && TemporaryPassword is not null;

    public static ResetPasswordServiceResult Success(string temporaryPassword) =>
        new(temporaryPassword, new Dictionary<string, string>());

    public static ResetPasswordServiceResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });

    public static ResetPasswordServiceResult NotFound() =>
        new(null, new Dictionary<string, string>(), false);
}
