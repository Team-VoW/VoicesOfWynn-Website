namespace VoW.Api.Contracts.Accounts;

public sealed record AccountDetailsResponse(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? DiscordId,
    string? Email,
    bool PublicEmail,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? Instagram,
    string? Github,
    string? CastingCallClub,
    string? Bio,
    string? Lore,
    bool ForcePasswordChange,
    bool SystemAdmin,
    IReadOnlyCollection<int> RoleIds);
