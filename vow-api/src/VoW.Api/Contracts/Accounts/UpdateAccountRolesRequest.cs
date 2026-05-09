namespace VoW.Api.Contracts.Accounts;

public sealed record UpdateAccountRolesRequest(
    IReadOnlyCollection<int>? RoleIds);
