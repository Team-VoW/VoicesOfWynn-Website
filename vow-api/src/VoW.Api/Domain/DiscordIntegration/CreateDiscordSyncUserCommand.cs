namespace VoW.Api.Domain.DiscordIntegration;

public sealed record CreateDiscordSyncUserCommand(
    string DisplayName,
    string PasswordHash,
    string DiscordId,
    string DiscordName);
