namespace VoW.Api.Services.Content;

public sealed record ContentMutationResult(
    int? Id,
    IReadOnlyDictionary<string, string> Errors,
    bool Found = true)
{
    public bool Succeeded => Errors.Count == 0;

    public static ContentMutationResult Success(int id) =>
        new(id, new Dictionary<string, string>());

    public static ContentMutationResult Success() =>
        new(null, new Dictionary<string, string>());

    public static ContentMutationResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });

    public static ContentMutationResult NotFound() =>
        new(null, new Dictionary<string, string>(), false);
}
