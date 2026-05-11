using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VoW.Api.Domain.Auth;

public sealed class RequireDiscordBotApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string BearerPrefix = "Bearer ";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedToken = configuration["DISCORD_BOT_API_KEY"] ?? configuration["DiscordBot:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        var token = authorization[BearerPrefix.Length..].Trim();
        if (!FixedTimeEquals(token, expectedToken))
        {
            context.Result = new UnauthorizedResult();
        }

        return Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}
