namespace VoW.Api.Contracts.DiscordIntegration;

public sealed record SyncDiscordUserRequest(
    string? DiscordId,
    string? DiscordName,
    string? DisplayName,
    string? AvatarUrl,
    IReadOnlyCollection<string>? RoleNames);
