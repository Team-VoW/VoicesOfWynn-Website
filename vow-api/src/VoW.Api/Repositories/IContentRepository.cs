using VoW.Api.Domain.Auth;
using VoW.Api.Domain.Content;

namespace VoW.Api.Repositories;

public interface IContentRepository
{
    Task<IReadOnlyCollection<ContentOption>> GetQuestsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ContentOption>> GetNpcsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ContentOption>> GetUsersByRolesAsync(
        IReadOnlyCollection<DiscordRoleId> roles,
        CancellationToken cancellationToken);

    Task<bool> QuestDegeneratedNameExistsAsync(string degeneratedName, CancellationToken cancellationToken);

    Task<bool> QuestDegeneratedNameExistsAsync(int exceptQuestId, string degeneratedName, CancellationToken cancellationToken);

    Task<bool> QuestExistsAsync(int questId, CancellationToken cancellationToken);

    Task<bool> NpcExistsAsync(int npcId, CancellationToken cancellationToken);

    Task<NpcArchiveData?> GetNpcArchiveDataAsync(int npcId, CancellationToken cancellationToken);

    Task<string?> GetQuestDegeneratedNameAsync(int questId, CancellationToken cancellationToken);

    Task<string?> GetNpcDegeneratedNameAsync(int npcId, CancellationToken cancellationToken);

    Task<int?> GetQuestIdByDegeneratedNameAsync(string degeneratedName, CancellationToken cancellationToken);

    Task<int?> GetQuestNpcIdByDegeneratedNameAsync(
        int questId,
        string degeneratedName,
        CancellationToken cancellationToken);

    Task<bool> NpcDegeneratedNameConflictsForLinkedQuestsAsync(
        int npcId,
        string degeneratedName,
        CancellationToken cancellationToken);

    Task<bool> NpcDegeneratedNameConflictsInQuestAsync(
        int questId,
        int npcId,
        string degeneratedName,
        CancellationToken cancellationToken);

    Task<bool> QuestHasNpcsAsync(int questId, CancellationToken cancellationToken);

    Task<bool> QuestNpcLinkExistsAsync(int questId, int npcId, CancellationToken cancellationToken);

    Task<bool> QuestNpcHasRecordingsAsync(int questId, int npcId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NpcRecording>> GetQuestNpcRecordingsAsync(
        int questId,
        int npcId,
        CancellationToken cancellationToken);

    Task<RecordingFile?> GetQuestNpcRecordingFileAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken);

    Task<RecordingFile?> GetRecordingByFileAsync(string fileName, CancellationToken cancellationToken);

    Task<RecordingConflict?> GetRecordingFileConflictAsync(
        string fileName,
        int questId,
        int npcId,
        int line,
        CancellationToken cancellationToken);

    Task<bool> UpdateRecordingFileAsync(
        int recordingId,
        string fileName,
        CancellationToken cancellationToken);

    Task<CreatedContent> InsertRecordingAsync(
        int questId,
        int npcId,
        int line,
        string fileName,
        CancellationToken cancellationToken);

    Task<bool> DeleteQuestNpcRecordingAsync(
        int questId,
        int npcId,
        int recordingId,
        CancellationToken cancellationToken);

    Task<ContentSearchPage> SearchAsync(ContentSearchCriteria criteria, CancellationToken cancellationToken);

    Task<CreatedContent> CreateQuestAsync(CreateQuestCommand command, string degeneratedName, CancellationToken cancellationToken);

    Task<CreatedContent> CreateNpcAsync(CreateNpcCommand command, string degeneratedName, CancellationToken cancellationToken);

    Task<bool> UpdateQuestAsync(int questId, string name, string degeneratedName, CancellationToken cancellationToken);

    Task<bool> UpdateQuestWriterAsync(int questId, int? writerUserId, CancellationToken cancellationToken);

    Task<bool> DeleteQuestAsync(int questId, CancellationToken cancellationToken);

    Task<bool> UpdateNpcAsync(int npcId, string name, string degeneratedName, CancellationToken cancellationToken);

    Task<bool> UpdateNpcVoiceActorAsync(int npcId, int? voiceActorUserId, CancellationToken cancellationToken);

    Task<bool> LinkNpcToQuestAsync(int questId, int npcId, CancellationToken cancellationToken);

    Task<bool> UpdateQuestNpcSoundEditorAsync(
        int questId,
        int npcId,
        int? soundEditorUserId,
        CancellationToken cancellationToken);

    Task<bool> UnlinkNpcFromQuestAsync(int questId, int npcId, CancellationToken cancellationToken);

    Task<int?> ArchiveNpcAsync(
        int npcId,
        bool createReplacement,
        IReadOnlyCollection<ArchivedRecordingFile> archivedRecordings,
        IReadOnlyCollection<int> deletedRecordingIds,
        CancellationToken cancellationToken);
}
