namespace VoW.Api.Domain.Accounts;

public sealed record UpdateAccountCommand(
    string DisplayName,
    string? PasswordHash,
    string? DiscordId,
    string? Email,
    bool? PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    string? Bio,
    string? Lore);
