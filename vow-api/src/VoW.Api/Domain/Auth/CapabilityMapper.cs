namespace VoW.Api.Domain.Auth;

public static class CapabilityMapper
{
    public const string ClaimType = "capability";
    public const string ReportsViewClaim = "reports.view";
    public const string ReportsManageClaim = "reports.manage";
    public const string AnalyticsViewClaim = "analytics.view";
    public const string ToolsScriptsClaim = "tools.scripts";
    public const string ToolsAudioAnalysisClaim = "tools.audio-analysis";
    public const string ContentManageClaim = "content.manage";
    public const string AccountsManageClaim = "accounts.manage";
    public const string SystemAdminClaim = "system.admin";
    public const string CastingVoteClaim = "casting.vote";
    public const string CastingManageClaim = "casting.manage";

    private static readonly Capability[] AllCapabilities =
    [
        Capability.ReportsView,
        Capability.ReportsManage,
        Capability.AnalyticsView,
        Capability.ToolsScripts,
        Capability.ToolsAudioAnalysis,
        Capability.ContentManage,
        Capability.AccountsManage,
        Capability.SystemAdmin,
        Capability.CastingVote,
        Capability.CastingManage
    ];

    private static readonly Capability[] CastManagerCapabilities =
    [
        Capability.ReportsView,
        Capability.ReportsManage,
        Capability.AnalyticsView,
        Capability.ToolsScripts,
        Capability.ToolsAudioAnalysis,
        Capability.ContentManage,
        Capability.CastingVote,
        Capability.CastingManage
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

    private static readonly HashSet<DiscordRoleId> CurrentStaffRoles =
    [
        DiscordRoleId.Moderator,
        DiscordRoleId.CastManager,
        DiscordRoleId.VoiceManager,
        DiscordRoleId.Developer,
        DiscordRoleId.Writer,
        DiscordRoleId.SoundEditor
    ];

    private static readonly HashSet<DiscordRoleId> SoundEditorRoles =
    [
        DiscordRoleId.SoundEditor,
        DiscordRoleId.TrialSoundEditor
    ];

    /// <summary>
    /// Everyone who votes on castings. Admin, Project Director and Cast Manager are covered by their
    /// full capability sets above; this list is also what the casting review counts as "staff".
    /// </summary>
    public static readonly IReadOnlyList<DiscordRoleId> CastingVoterRoles =
    [
        DiscordRoleId.ProjectDirector,
        DiscordRoleId.Admin,
        DiscordRoleId.CastManager,
        DiscordRoleId.VoiceManager
    ];

    public static IReadOnlyCollection<Capability> Map(IEnumerable<DiscordRoleId> roles)
    {
        var roleSet = roles.ToHashSet();
        if (roleSet.Overlaps(AdminRoles))
        {
            return AllCapabilities;
        }

        if (roleSet.Contains(DiscordRoleId.CastManager))
        {
            return CastManagerCapabilities;
        }

        var capabilities = new HashSet<Capability>();
        if (roleSet.Overlaps(ReportViewRoles))
        {
            capabilities.Add(Capability.ReportsView);
            capabilities.Add(Capability.AnalyticsView);
        }

        if (roleSet.Overlaps(CurrentStaffRoles))
        {
            capabilities.Add(Capability.ToolsScripts);
        }

        if (roleSet.Overlaps(SoundEditorRoles))
        {
            capabilities.Add(Capability.ToolsAudioAnalysis);
        }

        if (roleSet.Overlaps(CastingVoterRoles))
        {
            capabilities.Add(Capability.CastingVote);
        }

        return capabilities;
    }

    public static string ToClaimValue(Capability capability) => capability switch
    {
        Capability.ReportsView => ReportsViewClaim,
        Capability.ReportsManage => ReportsManageClaim,
        Capability.AnalyticsView => AnalyticsViewClaim,
        Capability.ToolsScripts => ToolsScriptsClaim,
        Capability.ToolsAudioAnalysis => ToolsAudioAnalysisClaim,
        Capability.ContentManage => ContentManageClaim,
        Capability.AccountsManage => AccountsManageClaim,
        Capability.SystemAdmin => SystemAdminClaim,
        Capability.CastingVote => CastingVoteClaim,
        Capability.CastingManage => CastingManageClaim,
        _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, null)
    };

    public static IEnumerable<Capability> GetAllCapabilities() => AllCapabilities;
}
