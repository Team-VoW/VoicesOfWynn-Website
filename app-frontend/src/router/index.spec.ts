import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import router from './index'
import { Capabilities, type Capability } from '@/lib/capabilities'
import { useAuthStore } from '@/stores/auth'

// Node 25 ships its own (unusable) localStorage global that shadows jsdom's, so
// the auth store gets a working in-memory one here.
function memoryStorage(): Storage {
  const entries = new Map<string, string>()
  return {
    get length() {
      return entries.size
    },
    key: (index: number) => [...entries.keys()][index] ?? null,
    getItem: (key: string) => entries.get(key) ?? null,
    setItem: (key: string, value: string) => void entries.set(key, value),
    removeItem: (key: string) => void entries.delete(key),
    clear: () => entries.clear(),
  }
}

function fakeAccessToken(capabilities: Capability[]) {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, '')
  return `${encode({ alg: 'none' })}.${encode({ display_name: 'Tester', capability: capabilities })}.sig`
}

function signIn(capabilities: Capability[] = [], forcePasswordChange = false) {
  const auth = useAuthStore()
  auth.setTokens(
    fakeAccessToken(capabilities),
    'refresh-token',
    new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    forcePasswordChange,
  )
}

async function visit(path: string) {
  await router.push(path).catch(() => {})
  await router.isReady()
  return router.currentRoute.value
}

beforeEach(async () => {
  vi.stubGlobal('localStorage', memoryStorage())
  // jsdom has no scrollTo, and navigating now asks for one.
  vi.stubGlobal('scrollTo', () => {})
  setActivePinia(createPinia())
  await router.replace('/')
})

describe('routing', () => {
  it('shows the public home page to anonymous visitors', async () => {
    const route = await visit('/')

    expect(route.name).toBe('home')
    expect(route.meta.public).toBe(true)
  })

  it('shows the public home page to signed-in visitors', async () => {
    signIn([Capabilities.ReportsView])

    const route = await visit('/')

    expect(route.name).toBe('home')
  })

  it('shows a not-found page for unknown URLs instead of bouncing home', async () => {
    const route = await visit('/this-page-does-not-exist')

    expect(route.name).toBe('not-found')
    expect(route.meta.public).toBe(true)
  })

  it('shows the public FAQ to anonymous visitors, hash and all', async () => {
    const route = await visit('/faq#data-processing')

    expect(route.name).toBe('faq')
    // The mod and the legacy download pages hand out this deep link; the view opens the
    // matching question from it.
    expect(route.hash).toBe('#data-processing')
  })

  it('shows the public quest index to anonymous visitors', async () => {
    const route = await visit('/contents')

    expect(route.name).toBe('contents')
    expect(route.meta.public).toBe(true)
  })

  it('addresses a quest by its degenerated name', async () => {
    const route = await visit('/contents/a-quest')

    expect(route.name).toBe('quest')
    expect(route.params.questName).toBe('a-quest')
  })

  it('reads an NPC url as an NPC rather than as a quest named "npc"', async () => {
    const route = await visit('/contents/npc/12')

    expect(route.name).toBe('npc')
    expect(route.params.npcId).toBe('12')
  })

  it('does not treat a non-numeric NPC id as an NPC', async () => {
    const route = await visit('/contents/npc/not-a-number')

    expect(route.name).toBe('not-found')
  })

  it('shows the public credits page to anonymous visitors', async () => {
    const route = await visit('/credits')

    expect(route.name).toBe('credits')
    expect(route.meta.public).toBe(true)
  })

  it('shows a public cast page to anonymous visitors', async () => {
    const route = await visit('/cast/42')

    expect(route.name).toBe('cast')
    expect(route.params.userId).toBe('42')
  })

  it('does not treat a non-numeric cast id as a contributor', async () => {
    const route = await visit('/cast/not-a-number')

    expect(route.name).toBe('not-found')
  })

  it('sends anonymous visitors of a protected route to login with a redirect', async () => {
    const route = await visit('/admin/reports')

    expect(route.name).toBe('login')
    expect(route.query.redirect).toBe('/admin/reports')
  })

  it('keeps the login page reachable while signed out', async () => {
    const route = await visit('/login')

    expect(route.name).toBe('login')
  })

  it('sends signed-in visitors away from the login page', async () => {
    signIn()

    const route = await visit('/login')

    expect(route.name).toBe('profile')
  })

  it('blocks protected routes the account has no capability for', async () => {
    signIn([Capabilities.AnalyticsView])

    const route = await visit('/admin/accounts')

    expect(route.name).toBe('analytics')
  })

  it('allows protected routes the account has the capability for', async () => {
    signIn([Capabilities.ReportsView])

    const route = await visit('/admin/reports')

    expect(route.name).toBe('reports')
  })

  // Cast Manager holds every other capability, so the Admin page is the one place where a
  // staff member with broad access must still be turned away.
  it('keeps the Admin page away from staff without system.admin', async () => {
    signIn([
      Capabilities.ReportsView,
      Capabilities.ReportsManage,
      Capabilities.AnalyticsView,
      Capabilities.ContentManage,
    ])

    const route = await visit('/admin/system')

    expect(route.name).not.toBe('admin')
  })

  it('opens the Admin page for accounts with system.admin', async () => {
    signIn([Capabilities.SystemAdmin])

    const route = await visit('/admin/system')

    expect(route.name).toBe('admin')
  })

  it('lets a voice manager vote on castings but not manage them', async () => {
    signIn([Capabilities.CastingVote])

    expect((await visit('/casting')).name).toBe('casting')
    expect((await visit('/admin/casting')).name).toBe('casting')
  })

  it('opens casting management and a round for casting managers', async () => {
    signIn([Capabilities.CastingVote, Capabilities.CastingManage])

    expect((await visit('/admin/casting')).name).toBe('casting-rounds')
    const round = await visit('/admin/casting/7')
    expect(round.name).toBe('casting-round-edit')
    expect(round.params.roundId).toBe('7')
  })

  it('keeps staff without the casting capability off the voting page', async () => {
    signIn([Capabilities.ReportsView])

    const route = await visit('/casting')

    expect(route.name).toBe('reports')
  })

  it('holds accounts that must change their password on the profile page', async () => {
    signIn([Capabilities.ReportsView], true)

    const route = await visit('/admin/reports')

    expect(route.name).toBe('profile')
  })

  it('still lets accounts that must change their password read the home page', async () => {
    signIn([Capabilities.ReportsView], true)

    const route = await visit('/')

    expect(route.name).toBe('home')
  })
})

describe('scroll position', () => {
  const scrollBehavior = router.options.scrollBehavior!

  const at = (path: string, hash = '') =>
    ({ path, hash, fullPath: `${path}${hash}` }) as RouteLocationNormalized

  it('starts a new page at the top rather than where the last one was left', () => {
    expect(scrollBehavior(at('/credits'), at('/contents'), null)).toEqual({ top: 0 })
  })

  it('puts the reader back where they were when they go back', () => {
    const saved = { left: 0, top: 940 }

    expect(scrollBehavior(at('/contents'), at('/credits'), saved)).toBe(saved)
  })

  it('leaves the page where it is when only the query changes', () => {
    expect(scrollBehavior(at('/contents'), at('/contents'), null)).toBe(false)
  })

  it('scrolls an anchor clear of the sticky header', () => {
    expect(scrollBehavior(at('/faq', '#data-processing'), at('/'), null)).toEqual({
      el: '#data-processing',
      top: 80,
    })
  })
})
