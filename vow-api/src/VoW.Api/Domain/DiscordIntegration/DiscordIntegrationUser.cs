using VoW.Api.Domain.Accounts;

namespace VoW.Api.Domain.DiscordIntegration;

public sealed record DiscordIntegrationUser(
    int UserId,
    string DisplayName,
    string DiscordId,
    string DiscordName,
    string Picture,
    PictureType PictureType,
    IReadOnlyCollection<string> RoleNames);
