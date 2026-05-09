namespace VoW.Api.Domain.Reports;

public static class ReportStatus
{
    public static readonly string[] Values =
    [
        "unprocessed",
        "forwarded",
        "rejected",
        "accepted",
        "fixed"
    ];

    public static readonly string DisplayList = string.Join(", ", Values);

    private static readonly HashSet<string> ValidValues = new(Values, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string status) => ValidValues.Contains(status);
}
