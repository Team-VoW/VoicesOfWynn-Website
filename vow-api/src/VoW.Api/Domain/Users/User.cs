namespace VoW.Api.Domain.Users;

public sealed record User(
    int UserId,
    string DiscordId,
    string DisplayName,
    bool SystemAdmin);
