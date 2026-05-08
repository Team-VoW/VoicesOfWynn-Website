using VoW.Api.Models;

namespace VoW.Api.Services;

public interface IExternalAuthProvider
{
    string Name { get; }

    string BuildLoginUrl();

    Task<ExternalUserIdentity> ExchangeCodeForIdentityAsync(string code, CancellationToken cancellationToken);
}
