import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { Capabilities, type Capability } from '@/lib/capabilities'
import { firstAccessibleAdminRoute } from '@/lib/adminRoutes'
import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    capability?: Capability
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: () => import('@/layouts/PublicLayout.vue'),
      meta: { public: true },
      children: [
        {
          path: '',
          name: 'home',
          component: () => import('@/features/home/views/HomeView.vue'),
        },
        {
          path: 'credits',
          name: 'credits',
          component: () => import('@/features/credits/views/CreditsView.vue'),
        },
        {
          path: 'cast/:userId(\\d+)',
          name: 'cast',
          component: () => import('@/features/cast/views/CastView.vue'),
        },
        {
          path: 'faq',
          name: 'faq',
          component: () => import('@/features/faq/views/FaqView.vue'),
        },
        {
          path: 'contents',
          name: 'contents',
          component: () => import('@/features/contents/views/ContentsView.vue'),
        },
        {
          // NPC pages are matched first so that an NPC id is never read as a quest name.
          path: 'contents/npc/:npcId(\\d+)',
          name: 'npc',
          component: () => import('@/features/contents/views/NpcView.vue'),
        },
        {
          // Quests are addressed by their degenerated name, the same URL the legacy site used.
          path: 'contents/:questName',
          name: 'quest',
          component: () => import('@/features/contents/views/QuestView.vue'),
        },
        {
          path: ':pathMatch(.*)*',
          name: 'not-found',
          component: () => import('@/features/errors/views/NotFoundView.vue'),
        },
      ],
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/features/auth/views/LoginView.vue'),
      meta: { public: true },
    },
    {
      path: '/auth/callback',
      name: 'callback',
      component: () => import('@/features/auth/views/CallbackView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      component: () => import('@/layouts/AppLayout.vue'),
      children: [
        {
          path: 'profile',
          name: 'profile',
          component: () => import('@/features/profile/views/ProfileView.vue'),
        },
        {
          path: 'casting',
          name: 'casting',
          component: () => import('@/features/casting/views/CastingView.vue'),
          meta: { capability: Capabilities.CastingVote },
        },
        {
          path: 'admin/casting',
          name: 'casting-rounds',
          component: () => import('@/features/casting/views/CastingRoundsView.vue'),
          meta: { capability: Capabilities.CastingManage },
        },
        {
          path: 'admin/casting/:roundId(\\d+)',
          name: 'casting-round-edit',
          component: () => import('@/features/casting/views/CastingRoundEditView.vue'),
          meta: { capability: Capabilities.CastingManage },
        },
        {
          path: 'admin/reports',
          name: 'reports',
          component: () => import('@/features/reports/views/ReportsSearchView.vue'),
          meta: { capability: Capabilities.ReportsView },
        },
        {
          path: 'admin/quest-feedback',
          name: 'quest-feedback',
          component: () => import('@/features/feedback/QuestFeedbackView.vue'),
          meta: { capability: Capabilities.ReportsView },
        },
        {
          path: 'admin/analytics',
          name: 'analytics',
          component: () => import('@/features/analytics/views/DailyUsageAnalyticsView.vue'),
          meta: { capability: Capabilities.AnalyticsView },
        },
        {
          path: 'tools/scripts',
          name: 'script-tools',
          component: () => import('@/features/tools/views/ScriptToolsView.vue'),
          meta: { capability: Capabilities.ToolsScripts },
        },
        {
          path: 'tools/audio',
          name: 'audio-tools',
          component: () => import('@/features/tools/views/AudioAnalysisView.vue'),
          meta: { capability: Capabilities.ToolsAudioAnalysis },
        },
        {
          path: 'admin/content',
          name: 'content',
          component: () => import('@/features/content/views/ContentManageView.vue'),
          meta: { capability: Capabilities.ContentManage },
        },
        {
          // Project Director and Admin only: mod bootup configuration and analytics aggregation.
          path: 'admin/system',
          name: 'admin',
          component: () => import('@/features/admin/views/AdminView.vue'),
          meta: { capability: Capabilities.SystemAdmin },
        },
        {
          path: 'admin/accounts',
          name: 'accounts',
          component: () => import('@/features/accounts/views/AccountsManageView.vue'),
          meta: { capability: Capabilities.AccountsManage },
        },
      ],
    },
  ],
  // Without this the reader keeps the scroll offset of the page they left, landing halfway down
  // the next one. Back and forward restore where they were; anything else starts at the top.
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) return savedPosition
    if (to.hash) return { el: to.hash, top: 80 }
    // Staying on the same page and only changing the query (a filter, a tab) is not a new page,
    // so it must not yank the reader back up.
    if (to.path === from.path) return false
    return { top: 0 }
  },
})

router.beforeEach((to: RouteLocationNormalized) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (!to.meta.public && auth.forcePasswordChange && to.name !== 'profile') {
    return { name: 'profile' }
  }
  if (to.meta.capability && !auth.hasCapability(to.meta.capability)) {
    return firstAccessibleAdminRoute(auth.hasCapability) ?? { name: 'profile' }
  }
  if (to.name === 'login' && auth.isAuthenticated) {
    return { name: 'profile' }
  }
  return true
})

export default router
