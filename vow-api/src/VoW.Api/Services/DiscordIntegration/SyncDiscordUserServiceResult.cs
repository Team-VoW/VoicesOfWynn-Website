using VoW.Api.Contracts.DiscordIntegration;

namespace VoW.Api.Services.DiscordIntegration;

public sealed record SyncDiscordUserServiceResult(
    SyncDiscordUserResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Response is not null && Errors.Count == 0;

    public static SyncDiscordUserServiceResult Success(SyncDiscordUserResponse response) =>
        new(response, new Dictionary<string, string>());

    public static SyncDiscordUserServiceResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });
}
