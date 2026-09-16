using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using VoW.Api.Contracts.Mod;
using VoW.Api.Domain.Mod;
using Xunit;

namespace VoW.Api.Tests;

/// <summary>
/// Boots the real application so the new endpoints are checked for routing and for the gate in
/// front of them. Every case here answers before any repository is touched, so no database is
/// needed.
/// </summary>
public sealed class EndpointAuthorizationTests
{
    private const string BotKey = "test-only-discord-bot-api-key";

    [Theory]
    [InlineData("POST", "/admin/analytics/aggregate")]
    [InlineData("GET", "/admin/mod/release")]
    [InlineData("PUT", "/admin/mod/release")]
    [InlineData("GET", "/admin/mod/broadcasts")]
    [InlineData("POST", "/admin/mod/broadcasts")]
    [InlineData("GET", "/admin/mod/fun-facts")]
    [InlineData("POST", "/admin/mod/fun-facts")]
    [InlineData("GET", "/admin/content/npcs/search?q=a")]
    public async Task StaffEndpointsRejectAnAnonymousCaller(string method, string path)
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/bot/reports/lines?statuses=accepted")]
    [InlineData("POST", "/bot/reports/lines/status")]
    [InlineData("DELETE", "/bot/reports/lines")]
    public async Task BotEndpointsRejectAWrongKey(string method, string path)
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-the-key");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Aggregation mutates; it must not be reachable by a GET the browser might prefetch.</summary>
    [Fact]
    public async Task AggregationIsPostOnly()
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/admin/analytics/aggregate");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task BotEndpointsRejectACallerWithNoKeyAtAll()
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var response = await client.GetAsync("/bot/reports/lines?statuses=accepted");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    // Anything but a 64-character lowercase hex digest, and an unparseable version.
    [InlineData("not-a-hash", "2.0.3")]
    [InlineData("ABCDEF", "2.0.3")]
    [InlineData("", "2.0.3")]
    public async Task ModBootupIsPublicButValidatesItsInput(string clientId, string modVersion)
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/mod/bootup", new { clientId, modVersion });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Aledar")]
    [InlineData("a line", null)]
    public async Task ReportIngestionIsPublicButValidatesItsInput(string chatMessage, string? npcName)
    {
        await using var app = new Api();
        using var client = app.CreateClient();

        var body = chatMessage.Length == 0
            ? (object)new { chatMessage, npcName }
            : new { chatMessage = new string('x', 320), npcName };
        var response = await client.PostAsJsonAsync("/reports", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The action is matched as a literal string by mod clients that cannot be corrected once
    /// shipped, so the exact casing on the wire is part of the contract - not whatever the globally
    /// registered enum converter happens to produce.
    /// </summary>
    [Theory]
    [InlineData(ModUpdateAction.None, "none")]
    [InlineData(ModUpdateAction.Notify, "notify")]
    [InlineData(ModUpdateAction.Disable, "disable")]
    public void TheUpdateActionSerializesLowercase(ModUpdateAction action, string expected)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());

        var json = JsonSerializer.Serialize(
            new ModUpdateResponse(action, "2.0.3", "https://a", "https://b"),
            options);

        Assert.Contains($"\"action\":\"{expected}\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"\"{expected}\"", json, StringComparison.Ordinal);
    }

    private sealed class Api : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JWT_SECRET", "test-only-jwt-secret-with-at-least-32-bytes");
            builder.UseSetting("DISCORD_BOT_API_KEY", BotKey);
        }
    }
}
