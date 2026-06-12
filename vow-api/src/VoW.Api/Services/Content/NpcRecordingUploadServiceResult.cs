using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public sealed record NpcRecordingUploadServiceResult(
    UploadNpcRecordingsResponse? Response,
    IReadOnlyDictionary<string, string> Errors,
    bool Found = true)
{
    public bool Succeeded => Errors.Count == 0;

    public static NpcRecordingUploadServiceResult Success(UploadNpcRecordingsResponse response) =>
        new(response, new Dictionary<string, string>());

    public static NpcRecordingUploadServiceResult Invalid(string field, string message) =>
        new(null, new Dictionary<string, string> { [field] = message });

    public static NpcRecordingUploadServiceResult NotFound() =>
        new(null, new Dictionary<string, string>(), false);
}
