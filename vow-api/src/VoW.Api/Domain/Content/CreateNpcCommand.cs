namespace VoW.Api.Domain.Content;

public sealed record CreateNpcCommand(
    string Name,
    int? VoiceActorUserId,
    IReadOnlyCollection<CreateNpcQuestAssignment> QuestAssignments);
