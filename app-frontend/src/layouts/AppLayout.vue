<script setup lang="ts">
import { storeToRefs } from 'pinia'
import {
  AudioLines,
  ChartLine,
  FileText,
  LayoutDashboard,
  Megaphone,
  Mic,
  ScrollText,
  Shield,
  User,
  Users,
} from 'lucide-vue-next'
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { TooltipProvider } from '@/components/ui/tooltip'
import { Capabilities, type Capability } from '@/lib/capabilities'
import { queryClient } from '@/lib/queryClient'
import { useAuthStore } from '@/stores/auth'
import { useSilentRefresh } from '@/features/auth/useSilentRefresh'
import { useOpenCastingRounds } from '@/features/casting/queries'

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
    label: 'Quest Feedback',
    to: '/admin/quest-feedback',
    routeName: 'quest-feedback',
    capability: Capabilities.ReportsView,
    icon: FileText,
  },
  {
    label: 'Analytics',
    to: '/admin/analytics',
    routeName: 'analytics',
    capability: Capabilities.AnalyticsView,
    icon: ChartLine,
  },
  {
    label: 'Script tool',
    to: '/tools/scripts',
    routeName: 'script-tools',
    capability: Capabilities.ToolsScripts,
    icon: ScrollText,
  },
  {
    label: 'Audio check',
    to: '/tools/audio',
    routeName: 'audio-tools',
    capability: Capabilities.ToolsAudioAnalysis,
    icon: AudioLines,
  },
  {
    label: 'Casting',
    to: '/casting',
    routeName: 'casting',
    capability: Capabilities.CastingVote,
    icon: Mic,
  },
  {
    label: 'Manage castings',
    to: '/admin/casting',
    routeName: 'casting-rounds',
    capability: Capabilities.CastingManage,
    icon: Megaphone,
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
  {
    label: 'Admin',
    to: '/admin/system',
    routeName: 'admin',
    capability: Capabilities.SystemAdmin,
    icon: Shield,
  },
]

// Characters the voter has not marked done yet, across every open round.
const canVote = computed(() => auth.hasCapability(Capabilities.CastingVote))
const { data: openCastings } = useOpenCastingRounds(canVote)
const castingTodo = computed(() =>
  (openCastings.value?.rounds ?? [])
    .filter((round) => round.votingOpen)
    .reduce((sum, round) => sum + round.characterCount - round.doneCount, 0),
)

const activeRouteNames: Record<string, string[]> = { 'casting-rounds': ['casting-round-edit'] }

function isActive(routeName: string) {
  return route.name === routeName || (activeRouteNames[routeName]?.includes(String(route.name)) ?? false)
}

const visibleNavItems = computed(() =>
  navItems.filter((item) => !item.capability || auth.hasCapability(item.capability)),
)

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
        <aside
          class="sticky top-0 hidden h-screen w-64 shrink-0 border-r border-[--brand-violet]/15 md:flex md:flex-col"
          style="background: var(--brand-gradient-soft)"
        >
          <RouterLink :to="{ name: 'home' }" class="flex items-center gap-3 px-5 py-5">
            <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-9 shrink-0" />
            <span class="font-display text-base tracking-wide">Voices of Wynn</span>
          </RouterLink>
          <nav class="flex flex-1 flex-col gap-1 overflow-y-auto px-3 py-2">
            <RouterLink
              v-for="item in visibleNavItems"
              :key="item.routeName"
              :to="item.to"
              class="relative flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-white/60 hover:text-foreground"
              :class="
                isActive(item.routeName)
                  ? 'bg-white/75 text-foreground font-semibold shadow-sm before:absolute before:left-0 before:top-1.5 before:bottom-1.5 before:w-1 before:rounded-full before:bg-[linear-gradient(180deg,#a340c4,#ff6b9d)]'
                  : ''
              "
            >
              <component :is="item.icon" class="size-4" aria-hidden="true" />
              <span class="flex-1">{{ item.label }}</span>
              <span
                v-if="item.routeName === 'casting' && castingTodo > 0"
                class="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary"
                :aria-label="`${castingTodo} characters left to vote on`"
              >
                {{ castingTodo }}
              </span>
            </RouterLink>
          </nav>
          <div class="border-t border-[--brand-violet]/15 px-5 py-4 text-sm">
            <p v-if="displayName" class="truncate text-muted-foreground">{{ displayName }}</p>
            <Button variant="ghost" size="sm" class="mt-2 px-0" @click="logout">Logout</Button>
          </div>
        </aside>

        <div class="flex min-w-0 flex-1 flex-col">
          <header
            class="sticky top-0 z-20 border-b border-[--brand-violet]/15 bg-background md:hidden"
          >
            <div class="flex items-center justify-between px-4 py-3">
              <RouterLink :to="{ name: 'home' }" class="flex items-center gap-2">
                <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-8 shrink-0" />
                <span class="font-display text-base tracking-wide">Voices of Wynn</span>
              </RouterLink>
              <Button variant="ghost" size="sm" @click="logout">Logout</Button>
            </div>
            <nav class="flex gap-1 overflow-x-auto px-4 pb-3">
              <RouterLink
                v-for="item in visibleNavItems"
                :key="item.routeName"
                :to="item.to"
                class="flex shrink-0 items-center gap-2 rounded-full px-3 py-2 text-sm font-medium text-muted-foreground transition-colors"
                :class="
                  isActive(item.routeName)
                    ? 'text-foreground font-semibold shadow-sm bg-[linear-gradient(135deg,rgba(163,64,196,0.18),rgba(255,107,157,0.18))]'
                    : 'hover:bg-accent hover:text-accent-foreground'
                "
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
