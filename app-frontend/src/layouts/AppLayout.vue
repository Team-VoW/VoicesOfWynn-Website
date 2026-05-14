<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { ChartLine, FileText, LayoutDashboard, User, Users } from 'lucide-vue-next'
import { computed } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Capabilities, type Capability } from '@/lib/capabilities'
import { firstAccessibleAdminRoute } from '@/lib/adminRoutes'
import { queryClient } from '@/lib/queryClient'
import { useAuthStore } from '@/stores/auth'
import { useSilentRefresh } from '@/features/auth/useSilentRefresh'

const auth = useAuthStore()
const { displayName } = storeToRefs(auth)
const route = useRoute()
const router = useRouter()

useSilentRefresh()

const navItems: {
  label: string
  to: string
  routeName: string
  capability?: Capability
  icon: typeof LayoutDashboard
}[] = [
  {
    label: 'Profile',
    to: '/profile',
    routeName: 'profile',
    icon: User,
  },
  {
    label: 'Reports',
    to: '/admin/reports',
    routeName: 'reports',
    capability: Capabilities.ReportsView,
    icon: LayoutDashboard,
  },
  {
    label: 'Analytics',
    to: '/admin/analytics',
    routeName: 'analytics',
    capability: Capabilities.AnalyticsView,
    icon: ChartLine,
  },
  {
    label: 'Manage content',
    to: '/admin/content',
    routeName: 'content',
    capability: Capabilities.ContentManage,
    icon: FileText,
  },
  {
    label: 'Accounts',
    to: '/admin/accounts',
    routeName: 'accounts',
    capability: Capabilities.AccountsManage,
    icon: Users,
  },
]

const visibleNavItems = computed(() =>
  navItems.filter((item) => !item.capability || auth.hasCapability(item.capability)),
)
const homeRoute = computed(() => {
  if (auth.forcePasswordChange) return '/profile'
  const route = firstAccessibleAdminRoute(auth.hasCapability)
  return route?.name ? router.resolve(route).fullPath : '/profile'
})

async function logout() {
  auth.clear()
  queryClient.clear()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <TooltipProvider>
    <div class="min-h-screen bg-background text-foreground">
      <div class="flex min-h-screen">
        <aside class="sticky top-0 hidden h-screen w-64 shrink-0 border-r bg-muted/20 md:flex md:flex-col">
          <RouterLink :to="homeRoute" class="flex items-center gap-2 px-5 py-4 text-base font-semibold">
            <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-8 shrink-0" />
            <span>Voices of Wynn</span>
          </RouterLink>
          <nav class="flex flex-1 flex-col gap-1 overflow-y-auto px-3 py-2">
            <RouterLink
              v-for="item in visibleNavItems"
              :key="item.routeName"
              :to="item.to"
              class="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
              :class="{ 'bg-accent text-accent-foreground': route.name === item.routeName }"
            >
              <component :is="item.icon" class="size-4" aria-hidden="true" />
              <span>{{ item.label }}</span>
            </RouterLink>
          </nav>
          <div class="border-t px-5 py-4 text-sm">
            <p v-if="displayName" class="truncate text-muted-foreground">{{ displayName }}</p>
            <Button variant="ghost" size="sm" class="mt-2 px-0" @click="logout">Logout</Button>
          </div>
        </aside>

        <div class="flex min-w-0 flex-1 flex-col">
          <header class="sticky top-0 z-20 border-b bg-background md:hidden">
            <div class="flex items-center justify-between px-4 py-3">
              <RouterLink :to="homeRoute" class="flex items-center gap-2 text-base font-semibold">
                <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-8 shrink-0" />
                <span>Voices of Wynn</span>
              </RouterLink>
              <Button variant="ghost" size="sm" @click="logout">Logout</Button>
            </div>
            <nav class="flex gap-1 overflow-x-auto px-4 pb-3">
              <RouterLink
                v-for="item in visibleNavItems"
                :key="item.routeName"
                :to="item.to"
                class="flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground"
                :class="{ 'bg-accent text-accent-foreground': route.name === item.routeName }"
              >
                <component :is="item.icon" class="size-4" aria-hidden="true" />
                <span>{{ item.label }}</span>
              </RouterLink>
            </nav>
          </header>

          <main class="min-w-0 px-4 py-6 md:px-6">
            <RouterView />
          </main>
        </div>
      </div>
    </div>
  </TooltipProvider>
</template>
