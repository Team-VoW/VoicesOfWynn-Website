namespace VoW.Api.Domain.Accounts;

public sealed record UpdateSelfProfileCommand(
    string DisplayName,
    string? Email,
    bool PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    string? Bio,
    string? Lore);
