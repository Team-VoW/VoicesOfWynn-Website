namespace VoW.Api.Contracts.Accounts;

public sealed record CreateAccountRequest(
    string? DisplayName,
    string? DiscordId,
    string? Discord,
    string? CastingCallClub);
