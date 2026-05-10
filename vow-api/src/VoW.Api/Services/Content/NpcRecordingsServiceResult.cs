using VoW.Api.Contracts.Content;

namespace VoW.Api.Services.Content;

public sealed record NpcRecordingsServiceResult(
    IReadOnlyCollection<NpcRecordingResponse> Recordings,
    bool Found = true)
{
    public static NpcRecordingsServiceResult Success(IReadOnlyCollection<NpcRecordingResponse> recordings) =>
        new(recordings);

    public static NpcRecordingsServiceResult NotFound() =>
        new([], false);
}
