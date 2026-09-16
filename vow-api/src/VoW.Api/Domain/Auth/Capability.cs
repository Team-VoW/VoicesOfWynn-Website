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
    SystemAdmin
}
