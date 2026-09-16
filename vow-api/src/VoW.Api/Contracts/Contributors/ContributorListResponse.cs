namespace VoW.Api.Contracts.Contributors;

public sealed record ContributorListResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<ContributorSummaryResponse> Results);

public sealed record ContributorSummaryResponse(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string? Lore,
    // The role the contributor is credited under, which is what the credits page groups by.
    ContributorRoleResponse TopRole,
    IReadOnlyCollection<ContributorRoleResponse> Roles);
