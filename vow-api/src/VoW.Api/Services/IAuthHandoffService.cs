using VoW.Api.Contracts.Auth;

namespace VoW.Api.Services;

public interface IAuthHandoffService
{
    string Create(AuthTokenResponse tokens);

    AuthTokenResponse? Consume(string code);
}
