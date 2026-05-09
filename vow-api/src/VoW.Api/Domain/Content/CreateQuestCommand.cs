namespace VoW.Api.Domain.Content;

public sealed record CreateQuestCommand(string Name, int? WriterUserId);
