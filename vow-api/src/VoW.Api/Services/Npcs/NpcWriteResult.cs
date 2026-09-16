namespace VoW.Api.Services.Npcs;

/// <summary>
/// Outcome of an anonymous write. Mirrors the shape of <c>ContentMutationResult</c> so controllers
/// translate it the same way, with a rate-limit case the admin endpoints do not need.
/// </summary>
public sealed record NpcWriteResult<T>(
    bool Succeeded,
    T? Value = default,
    bool Found = true,
    bool IsForbidden = false,
    bool IsRateLimited = false,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public static NpcWriteResult<T> Success(T value) => new(true, value);

    public static NpcWriteResult<T> NotFound() => new(false, Found: false);

    public static NpcWriteResult<T> Forbidden() => new(false, IsForbidden: true);

    public static NpcWriteResult<T> RateLimited() => new(false, IsRateLimited: true);

    public static NpcWriteResult<T> Invalid(IReadOnlyDictionary<string, string> errors) =>
        new(false, Errors: errors);

    public static NpcWriteResult<T> Invalid(string field, string message) =>
        Invalid(new Dictionary<string, string> { [field] = message });
}
