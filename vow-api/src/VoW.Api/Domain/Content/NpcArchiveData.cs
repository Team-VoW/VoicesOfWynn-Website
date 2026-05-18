namespace VoW.Api.Domain.Content;

public sealed record NpcArchiveData(
    int NpcId,
    string Name,
    string DegeneratedName,
    bool Archived,
    IReadOnlyCollection<RecordingFile> Recordings);

public sealed record ArchivedRecordingFile(int RecordingId, string FileName);
