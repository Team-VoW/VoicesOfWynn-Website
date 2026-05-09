namespace VoW.Api.Contracts.Content;

public sealed record NpcRecordingResponse(
    int RecordingId,
    int Line,
    string FileName,
    string Url);
