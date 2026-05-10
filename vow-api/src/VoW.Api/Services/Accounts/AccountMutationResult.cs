namespace VoW.Api.Services.Accounts;

public sealed record AccountMutationResult(
    IReadOnlyDictionary<string, string> Errors,
    bool Found = true,
    bool IsForbidden = false)
{
    public bool Succeeded => Found && !IsForbidden && Errors.Count == 0;

    public static AccountMutationResult Success() =>
        new(new Dictionary<string, string>());

    public static AccountMutationResult Invalid(string field, string message) =>
        new(new Dictionary<string, string> { [field] = message });

    public static AccountMutationResult NotFound() =>
        new(new Dictionary<string, string>(), false);

    public static AccountMutationResult Forbidden() =>
        new(new Dictionary<string, string>(), true, true);
}
