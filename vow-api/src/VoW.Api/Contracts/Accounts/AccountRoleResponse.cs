namespace VoW.Api.Contracts.Accounts;

public sealed record AccountRoleResponse(
    int Id,
    string Name,
    string Color,
    int Weight);
