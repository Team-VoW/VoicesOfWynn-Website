using VoW.Api.Contracts.Mod;

namespace VoW.Api.Services.Mod;

public sealed class ModBootupServiceResult
{
    private ModBootupServiceResult(ModBootupResponse? response, IReadOnlyDictionary<string, string> errors)
    {
        Response = response;
        Errors = errors;
    }

    public ModBootupResponse? Response { get; }

    public IReadOnlyDictionary<string, string> Errors { get; }

    public bool Succeeded => Errors.Count == 0 && Response is not null;

    public static ModBootupServiceResult Success(ModBootupResponse response) =>
        new(response, new Dictionary<string, string>());

    public static ModBootupServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
