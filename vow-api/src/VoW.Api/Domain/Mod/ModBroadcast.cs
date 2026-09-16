namespace VoW.Api.Domain.Mod;

public sealed record ModBroadcast(
    int Id,
    string Content,
    DateTime ActiveFrom,
    DateTime ActiveUntil,
    DateTime CreatedAt,
    int? CreatedBy);
