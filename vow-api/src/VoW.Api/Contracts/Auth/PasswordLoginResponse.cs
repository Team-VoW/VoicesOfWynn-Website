namespace VoW.Api.Contracts.Auth;

public sealed record PasswordLoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    bool ForcePasswordChange)
{
    public static PasswordLoginResponse From(AuthTokenResponse tokens, bool forcePasswordChange) =>
        new(tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresAt, forcePasswordChange);
}
