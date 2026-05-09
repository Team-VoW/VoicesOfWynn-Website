namespace VoW.Api.Contracts.Content;

public sealed record CreateNpcRequest(
    string? Name,
    int? VoiceActorUserId,
    IReadOnlyCollection<CreateNpcQuestAssignmentRequest>? QuestAssignments);
