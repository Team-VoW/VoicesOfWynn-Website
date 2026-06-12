namespace VoW.Api.Services.Content;

public sealed record NpcRecordingUpload(
    string FileName,
    string ContentType,
    long Length,
    Func<Stream> OpenReadStream);
