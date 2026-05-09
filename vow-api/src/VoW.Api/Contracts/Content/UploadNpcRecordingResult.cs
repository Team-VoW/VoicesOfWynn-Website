namespace VoW.Api.Contracts.Content;

public sealed record UploadNpcRecordingResult(
    string FileName,
    int Code,
    string Message,
    string Description,
    string? StoredFileName);
