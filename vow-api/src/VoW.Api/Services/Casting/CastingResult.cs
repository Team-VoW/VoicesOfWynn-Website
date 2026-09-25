namespace VoW.Api.Services.Casting;

public sealed record CastingResult(bool Found, IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Found && Errors.Count == 0;

    public static CastingResult Success() => new(true, new Dictionary<string, string>());

    public static CastingResult NotFound() => new(false, new Dictionary<string, string>());

    public static CastingResult Invalid(string field, string message) =>
        new(true, new Dictionary<string, string> { [field] = message });
}

/// <summary>A <see cref="CastingResult"/> that also carries a value when it succeeded.</summary>
public sealed record CastingResult<T>(CastingResult Result, T? Value)
{
    public bool Succeeded => Result.Succeeded;

    public static implicit operator CastingResult<T>(CastingResult result) => new(result, default);

    public static CastingResult<T> Success(T value) => new(CastingResult.Success(), value);
}
