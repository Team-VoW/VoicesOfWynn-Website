namespace VoW.Api.Domain.Accounts;

public sealed record AccountPasswordState(
    string? PasswordHash,
    bool ForcePasswordChange);
