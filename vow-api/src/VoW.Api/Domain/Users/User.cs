using VoW.Api.Domain.Auth;

namespace VoW.Api.Domain.Users;

public sealed record User(
    int UserId,
    string DiscordId,
    string DisplayName,
    IReadOnlyCollection<DiscordRoleId> Roles,
    IReadOnlyCollection<Capability> Capabilities);
