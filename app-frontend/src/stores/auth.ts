import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { jwtDecode } from 'jwt-decode'
import type { Capability } from '@/lib/capabilities'

// Tokens are kept in localStorage. We accept the XSS tradeoff for an admin-only
// SPA over the complexity of a server-side refresh-cookie flow.
const STORAGE_KEY = 'vow.auth'

interface PersistedAuth {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

interface AccessTokenClaims {
  sub?: string
  discord_id?: string
  display_name?: string
  exp?: number
  capability?: string | string[]
}

function load(): PersistedAuth | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as PersistedAuth) : null
  } catch {
    return null
  }
}

function save(value: PersistedAuth | null) {
  if (value) localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
  else localStorage.removeItem(STORAGE_KEY)
}

export const useAuthStore = defineStore('auth', () => {
  const initial = load()
  const accessToken = ref<string>(initial?.accessToken ?? '')
  const refreshToken = ref<string>(initial?.refreshToken ?? '')
  const expiresAt = ref<string>(initial?.expiresAt ?? '')

  const isAuthenticated = computed(() => {
    if (!accessToken.value) return false
    if (!expiresAt.value) return true
    return new Date(expiresAt.value).getTime() > Date.now()
  })

  const claims = computed<AccessTokenClaims | null>(() => {
    if (!accessToken.value) return null
    try {
      return jwtDecode<AccessTokenClaims>(accessToken.value)
    } catch {
      return null
    }
  })

  const displayName = computed(() => claims.value?.display_name ?? '')

  const capabilities = computed<string[]>(() => {
    const c = claims.value?.capability
    if (Array.isArray(c)) return c
    if (typeof c === 'string' && c) return [c]
    return []
  })

  function hasCapability(name: Capability) {
    return capabilities.value.includes(name)
  }

  function persist() {
    if (accessToken.value && refreshToken.value) {
      save({
        accessToken: accessToken.value,
        refreshToken: refreshToken.value,
        expiresAt: expiresAt.value,
      })
    } else {
      save(null)
    }
  }

  function setTokens(access: string, refresh: string, expiresAtIso: string) {
    accessToken.value = access
    refreshToken.value = refresh
    expiresAt.value = expiresAtIso
    persist()
  }

  function clear() {
    accessToken.value = ''
    refreshToken.value = ''
    expiresAt.value = ''
    persist()
  }

  return {
    accessToken,
    refreshToken,
    expiresAt,
    isAuthenticated,
    displayName,
    capabilities,
    hasCapability,
    setTokens,
    clear,
  }
})
