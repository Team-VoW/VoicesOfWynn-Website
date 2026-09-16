namespace VoW.Api.Services.Mod;

public sealed record ModConfigMutationResult(
    bool Found,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Found && Errors.Count == 0;

    public static ModConfigMutationResult Success() =>
        new(true, new Dictionary<string, string>());

    public static ModConfigMutationResult NotFound() =>
        new(false, new Dictionary<string, string>());

    public static ModConfigMutationResult Invalid(string field, string message) =>
        new(true, new Dictionary<string, string> { [field] = message });
}
