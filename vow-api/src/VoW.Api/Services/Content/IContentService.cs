using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public interface IContentService
{
    Task<ContentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);

    Task<ContentMutationResult> CreateQuestAsync(CreateQuestRequest request, CancellationToken cancellationToken);

    Task<ContentMutationResult> CreateNpcAsync(CreateNpcRequest request, CancellationToken cancellationToken);
}
