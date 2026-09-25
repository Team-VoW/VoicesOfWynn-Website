namespace VoW.Api.Domain.Auth;

public enum Capability
{
    ReportsView,
    ReportsManage,
    AnalyticsView,
    ToolsScripts,
    ToolsAudioAnalysis,
    ContentManage,
    AccountsManage,

    /// <summary>
    /// Project Director and Admin only - CastManager deliberately does not receive it.
    /// Gates the Admin page: analytics aggregation and the mod bootup configuration.
    /// </summary>
    SystemAdmin,

    /// <summary>Listening to auditions and voting in open casting rounds.</summary>
    CastingVote,

    /// <summary>Creating and running casting rounds, and the named vote review.</summary>
    CastingManage
}
