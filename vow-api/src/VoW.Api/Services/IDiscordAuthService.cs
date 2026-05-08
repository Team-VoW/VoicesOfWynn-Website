using VoW.Api.Models;

namespace VoW.Api.Services;

public interface IDiscordAuthService
{
    string BuildLoginUrl();

    Task<DiscordUserResponse> ExchangeCodeForUserAsync(string code, CancellationToken cancellationToken);
}
