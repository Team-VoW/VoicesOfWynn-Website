namespace VoW.Api.Contracts.Accounts;

public sealed record UpdateAccountRolesRequest
{
    public IReadOnlyCollection<int> RoleIds { get; init; } = Array.Empty<int>();
}
