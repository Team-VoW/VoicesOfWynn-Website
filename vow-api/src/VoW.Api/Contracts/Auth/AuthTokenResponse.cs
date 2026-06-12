namespace VoW.Api.Contracts.Auth;

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
