using VoW.Api.Models;

namespace VoW.Api.Services;

public interface IExternalAuthProvider
{
    string Name { get; }

    string BuildLoginUrl(string state);

    Task<ExternalUserIdentity> ExchangeCodeForIdentityAsync(string code, CancellationToken cancellationToken);
}
