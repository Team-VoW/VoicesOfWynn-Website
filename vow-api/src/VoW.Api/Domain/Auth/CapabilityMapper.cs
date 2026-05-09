namespace VoW.Api.Domain.Auth;

public static class CapabilityMapper
{
    public const string ClaimType = "capability";
    public const string ReportsViewClaim = "reports.view";
    public const string ReportsManageClaim = "reports.manage";
    public const string ContentManageClaim = "content.manage";
    public const string AccountsManageClaim = "accounts.manage";

    private static readonly Capability[] AllCapabilities =
    [
        Capability.ReportsView,
        Capability.ReportsManage,
        Capability.ContentManage,
        Capability.AccountsManage
    ];

    private static readonly HashSet<DiscordRoleId> AdminRoles =
    [
        DiscordRoleId.ProjectDirector,
        DiscordRoleId.Admin
    ];

    private static readonly HashSet<DiscordRoleId> ReportViewRoles =
    [
        DiscordRoleId.Moderator,
        DiscordRoleId.CastManager,
        DiscordRoleId.VoiceManager,
        DiscordRoleId.Developer,
        DiscordRoleId.Writer,
        DiscordRoleId.SoundEditor
    ];

    public static IReadOnlyCollection<Capability> Map(IEnumerable<DiscordRoleId> roles)
    {
        var roleSet = roles.ToHashSet();
        if (roleSet.Overlaps(AdminRoles))
        {
            return AllCapabilities;
        }

        var capabilities = new HashSet<Capability>();
        if (roleSet.Overlaps(ReportViewRoles))
        {
            capabilities.Add(Capability.ReportsView);
        }

        return capabilities;
    }

    public static string ToClaimValue(Capability capability) => capability switch
    {
        Capability.ReportsView => ReportsViewClaim,
        Capability.ReportsManage => ReportsManageClaim,
        Capability.ContentManage => ContentManageClaim,
        Capability.AccountsManage => AccountsManageClaim,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, null)
    };

    public static IEnumerable<Capability> GetAllCapabilities() => AllCapabilities;
}
