namespace VoW.Api.Domain.Casting;

/// <summary>Maps the snake_case MariaDB enum values of the casting tables to the domain enums.</summary>
public static class CastingEnumMapping
{
    public static string ToDb(CastingRoundStatus status) => status switch
    {
        CastingRoundStatus.Draft => "draft",
        CastingRoundStatus.Open => "open",
        CastingRoundStatus.Closed => "closed",
        CastingRoundStatus.Archived => "archived",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static CastingRoundStatus RoundStatusFromDb(string value) => value switch
    {
        "draft" => CastingRoundStatus.Draft,
        "open" => CastingRoundStatus.Open,
        "closed" => CastingRoundStatus.Closed,
        "archived" => CastingRoundStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDb(CastingSource source) => source switch
    {
        CastingSource.Manual => "manual",
        CastingSource.Ccc => "ccc",
        CastingSource.Discord => "discord",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };

    public static CastingSource SourceFromDb(string value) => value switch
    {
        "manual" => CastingSource.Manual,
        "ccc" => CastingSource.Ccc,
        "discord" => CastingSource.Discord,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToDb(CastingImportStatus status) => status switch
    {
        CastingImportStatus.Idle => "idle",
        CastingImportStatus.Running => "running",
        CastingImportStatus.Done => "done",
        CastingImportStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static CastingImportStatus ImportStatusFromDb(string value) => value switch
    {
        "idle" => CastingImportStatus.Idle,
        "running" => CastingImportStatus.Running,
        "done" => CastingImportStatus.Done,
        "failed" => CastingImportStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
