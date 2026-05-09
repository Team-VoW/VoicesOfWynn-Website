namespace VoW.Api.Domain.Accounts;

public sealed record AccountDetails(
    int UserId,
    string DisplayName,
    string Picture,
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
    bool SystemAdmin,
    IReadOnlyCollection<int> RoleIds);
