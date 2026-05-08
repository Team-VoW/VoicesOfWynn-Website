namespace VoW.Api.Models;

public sealed record User(
    int UserId,
    string DiscordId,
    string DisplayName,
    bool SystemAdmin);
