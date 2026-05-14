import { onBeforeUnmount, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { refreshAccessToken } from '@/api/auth'
import { ApiError } from '@/api/client'
import { queryClient } from '@/lib/queryClient'

const REFRESH_LEEWAY_MS = 60_000
const RETRY_DELAY_MS = 30_000

export function useSilentRefresh() {
  const auth = useAuthStore()
  const { expiresAt, refreshToken } = storeToRefs(auth)
  let timer: number | null = null

  function clearTimer() {
    if (timer !== null) {
      window.clearTimeout(timer)
      timer = null
    }
  }

  async function doRefresh() {
    const tokenAtStart = auth.refreshToken
    if (!tokenAtStart) return
    try {
      const res = await refreshAccessToken(tokenAtStart)
      if (auth.refreshToken !== tokenAtStart) return
      auth.setTokens(res.accessToken, res.refreshToken, res.expiresAt)
    } catch (err) {
      if (auth.refreshToken !== tokenAtStart) return
      // Only clear when the refresh token itself was rejected. Transient failures
      // (network, 5xx) leave the session intact and we'll retry shortly.
      if (err instanceof ApiError && (err.status === 400 || err.status === 401)) {
        auth.clear()
        queryClient.clear()
        return
      }
      timer = window.setTimeout(() => void doRefresh(), RETRY_DELAY_MS)
    }
  }

  function schedule() {
    clearTimer()
    if (!expiresAt.value || !refreshToken.value) return
    const delay = new Date(expiresAt.value).getTime() - Date.now() - REFRESH_LEEWAY_MS
    if (delay <= 0) {
      void doRefresh()
      return
    }
    timer = window.setTimeout(() => void doRefresh(), delay)
  }

  watch([expiresAt, refreshToken], schedule, { immediate: true })
  onBeforeUnmount(clearTimer)
}
