namespace VoW.Api.Domain.Accounts;

public sealed record CreateAccountCommand(
    string DisplayName,
    string PasswordHash,
    string? DiscordId,
    string? Discord,
    string? CastingCallClub);
