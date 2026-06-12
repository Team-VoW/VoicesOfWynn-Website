namespace VoW.Api.Contracts.Accounts;

public sealed record AccountSearchResult(
    int UserId,
    string DisplayName,
    string AvatarUrl,
    string DefaultAvatarUrl,
    string SocialSummary,
    IReadOnlyCollection<int> RoleIds);
