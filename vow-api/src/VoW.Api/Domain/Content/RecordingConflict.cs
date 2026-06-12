namespace VoW.Api.Domain.Content;

public sealed record RecordingConflict(
    int RecordingId,
    int QuestId,
    string QuestName,
    int NpcId,
    string NpcName,
    short Line,
    string FileName);
