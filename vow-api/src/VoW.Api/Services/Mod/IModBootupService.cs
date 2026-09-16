using VoW.Api.Contracts.Mod;

namespace VoW.Api.Services.Mod;

public interface IModBootupService
{
    Task<ModBootupServiceResult> BootupAsync(
        ModBootupRequest request,
        string callerIp,
        CancellationToken cancellationToken);
}
