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

    /// <summary>
    /// Uploads the quest script content for the specified quest.
    /// </summary>
    /// <param name="content">
    /// The caller retains ownership of this stream and must dispose it after the call.
    /// The implementation may read it asynchronously and does not require it to be seekable.
    /// </param>
    Task<ContentMutationResult> UploadQuestScriptAsync(
        int questId,
        Stream content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uploads and normalizes the NPC image content for the specified NPC.
    /// </summary>
    /// <param name="content">
    /// The caller retains ownership of this stream and must dispose it after the call.
    /// The implementation may read it asynchronously and will seek to the beginning when the stream supports seeking.
    /// </param>
    Task<ContentMutationResult> UploadNpcImageAsync(
        int npcId,
        Stream content,
        CancellationToken cancellationToken);

    Task<NpcRecordingUploadServiceResult> UploadNpcRecordingsAsync(
        int questId,
        int npcId,
        IReadOnlyCollection<NpcRecordingUpload> recordings,
        bool overwrite,
        CancellationToken cancellationToken);

    Task<NpcRecordingsServiceResult> GetNpcRecordingsAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken);

    Task<ContentMutationResult> DeleteNpcRecordingAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken);
}
