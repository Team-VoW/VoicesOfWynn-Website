namespace VoW.Api.Contracts.Content;

public sealed record ContentSearchRequest(
    string? Quest,
    string? Npc,
    int Page = 1,
    int PageSize = 25);
