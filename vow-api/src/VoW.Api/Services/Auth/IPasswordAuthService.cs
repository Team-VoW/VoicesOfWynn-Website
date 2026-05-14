using VoW.Api.Contracts.Auth;

namespace VoW.Api.Services.Auth;

public interface IPasswordAuthService
{
    Task<PasswordAuthResult> LoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken);
}
