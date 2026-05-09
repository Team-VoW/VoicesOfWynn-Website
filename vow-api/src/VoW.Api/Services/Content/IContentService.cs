using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public interface IContentService
{
    Task<ContentOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken);

    Task<ContentSearchServiceResult> SearchAsync(ContentSearchRequest request, CancellationToken cancellationToken);

    Task<ContentMutationResult> CreateQuestAsync(CreateQuestRequest request, CancellationToken cancellationToken);

    Task<ContentMutationResult> CreateNpcAsync(CreateNpcRequest request, CancellationToken cancellationToken);

    Task<ContentMutationResult> UpdateQuestAsync(
        int questId,
        UpdateContentNameRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UpdateQuestWriterAsync(
        int questId,
        UpdateQuestWriterRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> DeleteQuestAsync(int questId, CancellationToken cancellationToken);

    Task<ContentMutationResult> UpdateNpcAsync(
        int npcId,
        UpdateContentNameRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UpdateNpcVoiceActorAsync(
        int npcId,
        UpdateNpcVoiceActorRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> LinkNpcToQuestAsync(
        int questId,
        LinkQuestNpcRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UpdateQuestNpcSoundEditorAsync(
        int questId,
        int npcId,
        UpdateQuestNpcSoundEditorRequest request,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UnlinkNpcFromQuestAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UploadQuestScriptAsync(
        int questId,
        Stream content,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> UploadNpcImageAsync(
        int npcId,
        Stream content,
        CancellationToken cancellationToken);
}
