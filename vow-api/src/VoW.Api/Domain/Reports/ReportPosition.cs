namespace VoW.Api.Domain.Reports;

/// <summary>
/// Where the player stood when the line was heard. The columns are MEDIUMINT and nullable, so a
/// report imported from a script has no position at all.
/// </summary>
public sealed record ReportPosition(int X, int Y, int Z)
{
    public const int MinCoordinate = -8_388_608;
    public const int MaxCoordinate = 8_388_607;

    public static bool IsInRange(int value) => value is >= MinCoordinate and <= MaxCoordinate;
}
