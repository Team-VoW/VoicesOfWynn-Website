using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public interface INpcLookupService
{
    Task<NpcLookupResponse> SearchAsync(NpcLookupRequest request, CancellationToken cancellationToken);
}
