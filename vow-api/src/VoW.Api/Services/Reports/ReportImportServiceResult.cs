using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public sealed record ReportImportServiceResult(
    ImportVoicedLinesResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Errors.Count == 0;

    public static ReportImportServiceResult Success(ImportVoicedLinesResponse response) =>
        new(response, new Dictionary<string, string>());

    public static ReportImportServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
