namespace VoW.Api.Domain.Mod;

public sealed record FunFact(
    int Id,
    string Slug,
    string Content,
    bool Active,
    DateTime CreatedAt);
