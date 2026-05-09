namespace VoW.Api.Domain.Auth;

public sealed record ExternalUserIdentity(
    string Provider,
    string Id,
    string DisplayName);
