using VoW.Api.Domain.Accounts;

namespace VoW.Api.Contracts.DiscordIntegration;

public sealed record DiscordIntegrationUserResponse(
    int UserId,
    string DisplayName,
    string DiscordId,
    string DiscordName,
    string AvatarUrl,
    PictureType AvatarType,
    IReadOnlyCollection<string> RoleNames);
