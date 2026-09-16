using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public sealed record LineQueryServiceResult(
    LineQueryResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Response is not null && Errors.Count == 0;

    public static LineQueryServiceResult Success(LineQueryResponse response) =>
        new(response, new Dictionary<string, string>());

    public static LineQueryServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
