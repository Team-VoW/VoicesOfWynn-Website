namespace VoW.Api.Contracts.DiscordIntegration;

public sealed record SyncDiscordUserResponse(
    int UserId,
    bool Created,
    string? TemporaryPassword = null);
