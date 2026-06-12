using VoW.Api.Contracts.Analytics;

namespace VoW.Api.Services.Analytics;

public sealed class DailyUsageServiceResult
{
    private DailyUsageServiceResult(DailyUsageResponse? response, IReadOnlyDictionary<string, string> errors)
    {
        Response = response;
        Errors = errors;
    }

    public DailyUsageResponse? Response { get; }

    public IReadOnlyDictionary<string, string> Errors { get; }

    public bool Succeeded => Errors.Count == 0 && Response is not null;

    public static DailyUsageServiceResult Success(DailyUsageResponse response) =>
        new(response, new Dictionary<string, string>());

    public static DailyUsageServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
