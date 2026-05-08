using System.ComponentModel.DataAnnotations;

namespace VoW.Api.Models;

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed record RefreshTokenRequest(
    [Required] string RefreshToken);

public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record DiscordTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope);

public sealed record DiscordUserResponse(
    string Id,
    string Username,
    string? GlobalName);
