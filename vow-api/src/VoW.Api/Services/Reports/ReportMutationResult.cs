namespace VoW.Api.Services.Reports;

public sealed record ReportMutationResult(
    bool Found,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Found && Errors.Count == 0;

    public static ReportMutationResult Success() =>
        new(true, new Dictionary<string, string>());

    public static ReportMutationResult NotFound() =>
        new(false, new Dictionary<string, string>());

    public static ReportMutationResult Invalid(string field, string message) =>
        new(true, new Dictionary<string, string> { [field] = message });
}
