namespace VoW.Api.Contracts.Accounts;

public sealed record UpdateAccountRequest(
    string? DisplayName,
    string? Password,
    string? DiscordId,
    string? Email,
    bool? PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    string? Bio,
    string? Lore);
