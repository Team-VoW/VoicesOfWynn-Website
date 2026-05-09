using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public sealed record ReportSearchServiceResult(
    ReportSearchResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static ReportSearchServiceResult Success(ReportSearchResponse response) =>
        new(response, new Dictionary<string, string>());

    public static ReportSearchServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
