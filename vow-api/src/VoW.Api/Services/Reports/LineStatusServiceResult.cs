using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public sealed record LineStatusServiceResult(
    SetLineStatusResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Response is not null && Errors.Count == 0;

    public static LineStatusServiceResult Success(SetLineStatusResponse response) =>
        new(response, new Dictionary<string, string>());

    public static LineStatusServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
