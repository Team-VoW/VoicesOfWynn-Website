using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public sealed record ContentSearchServiceResult(
    ContentSearchResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static ContentSearchServiceResult Success(ContentSearchResponse response) =>
        new(response, new Dictionary<string, string>());

    public static ContentSearchServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
