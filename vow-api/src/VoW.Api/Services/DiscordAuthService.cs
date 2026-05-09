using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using VoW.Api.Models;

namespace VoW.Api.Services;

public sealed class DiscordAuthService(HttpClient httpClient, IConfiguration configuration) : IExternalAuthProvider
{
    public string Name => "discord";

    public string BuildLoginUrl(string state)
    {
        var clientId = GetRequired("DISCORD_CLIENT_ID");
        var redirectUri = GetRequired("DISCORD_REDIRECT_URI");

        return QueryHelpers.AddQueryString("https://discord.com/api/oauth2/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "identify",
            ["state"] = state
        });
    }

    public async Task<ExternalUserIdentity> ExchangeCodeForIdentityAsync(string code, CancellationToken cancellationToken)
    {
        var token = await ExchangeCodeAsync(code, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<DiscordApiUser>(cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("Discord user response was empty.");
        }

        return new ExternalUserIdentity(Name, user.Id, user.GlobalName ?? user.Username);
    }

    private async Task<DiscordTokenResponse> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var clientId = GetRequired("DISCORD_CLIENT_ID");
        var clientSecret = GetRequired("DISCORD_CLIENT_SECRET");
        var redirectUri = GetRequired("DISCORD_REDIRECT_URI");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        });

        using var response = await httpClient.PostAsync("https://discord.com/api/oauth2/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<DiscordApiToken>(cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException("Discord token response was empty.");
        }

        return new DiscordTokenResponse(token.AccessToken, token.TokenType, token.ExpiresIn, token.Scope);
    }

    private string GetRequired(string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} is not configured.")
            : value;
    }

    private sealed record DiscordApiToken(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string Scope);

    private sealed record DiscordApiUser(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("global_name")] string? GlobalName);
}
