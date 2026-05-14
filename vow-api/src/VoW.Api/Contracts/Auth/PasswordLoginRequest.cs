namespace VoW.Api.Contracts.Auth;

public sealed record PasswordLoginRequest(string? Username, string? Password);
