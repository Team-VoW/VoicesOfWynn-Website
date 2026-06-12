// Mirrors the backend `Capability` enum's claim values
// (vow-api/src/VoW.Api/Domain/Auth/CapabilityMapper.cs). Keep names in sync.
export const Capabilities = {
  ReportsView: 'reports.view',
  ReportsManage: 'reports.manage',
  AnalyticsView: 'analytics.view',
  ToolsScripts: 'tools.scripts',
  ToolsAudioAnalysis: 'tools.audio-analysis',
  ContentManage: 'content.manage',
  AccountsManage: 'accounts.manage',
} as const

export type Capability = (typeof Capabilities)[keyof typeof Capabilities]
