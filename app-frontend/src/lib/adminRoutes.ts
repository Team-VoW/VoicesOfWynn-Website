import { Capabilities, type Capability } from '@/lib/capabilities'

const adminRouteOrder = ['reports', 'analytics', 'content', 'accounts'] as const

type AdminRouteName = (typeof adminRouteOrder)[number]

export function firstAccessibleAdminRoute(
  hasCapability: (capability: Capability) => boolean,
): { name: AdminRouteName } | undefined {
  return adminRouteOrder
    .map((name) => ({ name }))
    .find((route) => {
      const capability = routeCapabilityByName[route.name]
      return capability ? hasCapability(capability) : false
    })
}

const routeCapabilityByName: Record<AdminRouteName, Capability> = {
  reports: Capabilities.ReportsView,
  analytics: Capabilities.AnalyticsView,
  content: Capabilities.ContentManage,
  accounts: Capabilities.AccountsManage,
}
