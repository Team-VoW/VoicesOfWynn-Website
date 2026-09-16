using VoW.Api.Contracts.Reports;

namespace VoW.Api.Services.Reports;

public sealed record LineReportSubmissionResult(
    SubmitLineReportResponse? Response,
    bool Created,
    bool IsRateLimited,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Response is not null && !IsRateLimited && Errors.Count == 0;

    public static LineReportSubmissionResult Success(SubmitLineReportResponse response, bool created) =>
        new(response, created, false, new Dictionary<string, string>());

    public static LineReportSubmissionResult RateLimited() =>
        new(null, false, true, new Dictionary<string, string>());

    public static LineReportSubmissionResult Invalid(string field, string message) =>
        new(null, false, false, new Dictionary<string, string> { [field] = message });
}
