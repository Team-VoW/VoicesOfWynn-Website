using VoW.Api.Contracts.DiscordIntegration;

namespace VoW.Api.Services.DiscordIntegration;

public sealed record SyncDiscordUserServiceResult(
    SyncDiscordUserResponse? Response,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Succeeded => Response is not null && Errors.Count == 0;

    public static SyncDiscordUserServiceResult Success(SyncDiscordUserResponse response) =>
        new(response, new Dictionary<string, string>());

    public static SyncDiscordUserServiceResult Invalid(IReadOnlyDictionary<string, string> errors) =>
        new(null, errors);

    public static SyncDiscordUserServiceResult Invalid(params KeyValuePair<string, string>[] errors) =>
        new(null, new Dictionary<string, string>(errors));

    public static SyncDiscordUserServiceResult Invalid(string field, string message) =>
        Invalid(new KeyValuePair<string, string>(field, message));
}
