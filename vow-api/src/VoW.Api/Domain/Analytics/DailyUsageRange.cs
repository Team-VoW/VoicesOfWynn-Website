namespace VoW.Api.Domain.Analytics;

public static class DailyUsageRange
{
    public const string Last30Days = "30";
    public const string Last90Days = "90";
    public const string Last365Days = "365";
    public const string All = "all";

    private static readonly HashSet<string> ValidRanges = [Last30Days, Last90Days, Last365Days, All];

    public static bool IsValid(string value) => ValidRanges.Contains(value);

    public static int? ToDays(string value) => value switch
    {
        Last30Days => 30,
        Last90Days => 90,
        Last365Days => 365,
        All => null,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
