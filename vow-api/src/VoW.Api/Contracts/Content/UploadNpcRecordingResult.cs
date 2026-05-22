namespace VoW.Api.Contracts.Content;

public sealed record UploadNpcRecordingResult(
    string FileName,
    int Code,
    string Message,
    string Description,
    string? StoredFileName,
    RecordingConflictResponse? Conflict);

public sealed record RecordingConflictResponse(
    int RecordingId,
    int QuestId,
    string QuestName,
    int NpcId,
    string NpcName,
    short Line,
    string FileName);
