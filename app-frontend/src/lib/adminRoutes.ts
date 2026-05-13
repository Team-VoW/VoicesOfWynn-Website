import type { Router } from 'vue-router'
import { Capabilities, type Capability } from '@/lib/capabilities'

const adminRouteOrder = ['reports', 'analytics', 'content', 'accounts'] as const

export function firstAccessibleAdminRoute(hasCapability: (capability: Capability) => boolean) {
  return adminRouteOrder
    .map((name) => ({ name }))
    .find((route) => {
      const capability = routeCapabilityByName[route.name]
      return capability ? hasCapability(capability) : false
    })
}

export function canAccessRedirect(
  router: Router,
  redirect: string,
  hasCapability: (capability: Capability) => boolean,
) {
  if (!redirect.startsWith('/admin/')) {
    return false
  }

  const target = router.resolve(redirect)
  const capability = target.matched.find((match) => match.meta.capability)?.meta.capability
  return capability ? hasCapability(capability) : false
}

const routeCapabilityByName: Record<(typeof adminRouteOrder)[number], Capability> = {
  reports: Capabilities.ReportsView,
  analytics: Capabilities.AnalyticsView,
  content: Capabilities.ContentManage,
  accounts: Capabilities.AccountsManage,
}
