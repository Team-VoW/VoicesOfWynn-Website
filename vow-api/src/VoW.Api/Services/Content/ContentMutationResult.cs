namespace VoW.Api.Services.Content;

public sealed record ContentMutationResult(
    int? Id,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static ContentMutationResult Success(int id) =>
        new(id, new Dictionary<string, string>());

    public static ContentMutationResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
