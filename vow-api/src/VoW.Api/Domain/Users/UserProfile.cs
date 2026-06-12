using VoW.Api.Domain.Auth;

namespace VoW.Api.Domain.Users;

public sealed record UserProfile(
    int UserId,
    string? DiscordId,
    string DisplayName,
    IReadOnlyCollection<DiscordRoleId> Roles);
