namespace VoW.Api.Domain.Content;

public sealed record ContentSearchCriteria(
    string? Quest,
    string? Npc,
    int Page,
    int PageSize);
