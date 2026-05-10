import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { Capabilities, type Capability } from '@/lib/capabilities'
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
          path: '',
          redirect: { name: 'reports' },
        },
        {
          path: 'admin/reports',
          name: 'reports',
          component: () => import('@/features/reports/views/ReportsSearchView.vue'),
          meta: { capability: Capabilities.ReportsView },
        },
        {
          path: 'admin/content',
          name: 'content',
          component: () => import('@/features/content/views/ContentManageView.vue'),
          meta: { capability: Capabilities.ContentManage },
        },
        {
          path: 'admin/accounts',
          name: 'accounts',
          component: () => import('@/features/accounts/views/AccountsManageView.vue'),
          meta: { capability: Capabilities.AccountsManage },
        },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: { name: 'reports' } },
  ],
})

router.beforeEach((to: RouteLocationNormalized) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (to.meta.capability && !auth.hasCapability(to.meta.capability)) {
    if (auth.hasCapability(Capabilities.ReportsView)) {
      return { name: 'reports' }
    }
    return { name: 'login' }
  }
  if (to.name === 'login' && auth.isAuthenticated && auth.hasCapability(Capabilities.ReportsView)) {
    return { name: 'reports' }
  }
  return true
})

export default router
