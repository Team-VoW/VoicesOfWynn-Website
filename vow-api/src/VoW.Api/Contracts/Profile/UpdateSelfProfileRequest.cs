namespace VoW.Api.Contracts.Profile;

public sealed record UpdateSelfProfileRequest(
    string DisplayName,
    string? Email,
    bool PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    string? Bio,
    string? Lore);
