namespace VoW.Api.Domain.Accounts;

public sealed record AccountSummary(
    int UserId,
    string DisplayName,
    string Picture,
    PictureType PictureType,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Email,
    string? Discord,
    string? Youtube,
    string? Twitter,
    string? CastingCallClub,
    IReadOnlyCollection<int> RoleIds);
