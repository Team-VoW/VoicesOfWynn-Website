using VoW.Api.Contracts.Auth;

namespace VoW.Api.Services.Auth;

public sealed record PasswordAuthResult(
    bool Succeeded,
    bool ForcePasswordChange,
    AuthTokenResponse? Tokens,
    IReadOnlyDictionary<string, string> Errors)
{
    public static PasswordAuthResult Unauthorized() => new(false, false, null, new Dictionary<string, string>());

    public static PasswordAuthResult Invalid(string field, string message) =>
        new(false, false, null, new Dictionary<string, string> { [field] = message });

    public static PasswordAuthResult Success(AuthTokenResponse tokens, bool forcePasswordChange) =>
        new(true, forcePasswordChange, tokens, new Dictionary<string, string>());
}
