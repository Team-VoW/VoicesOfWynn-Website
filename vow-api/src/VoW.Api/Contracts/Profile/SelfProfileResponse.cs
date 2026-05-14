namespace VoW.Api.Contracts.Profile;

public sealed record SelfProfileResponse(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Email,
    bool PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    string? Bio,
    string? Lore,
    bool ForcePasswordChange,
    bool PasswordChangeRequiresCurrentPassword);
