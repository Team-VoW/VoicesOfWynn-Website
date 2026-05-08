import { onBeforeUnmount, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import { refreshAccessToken } from '@/api/auth'

const REFRESH_LEEWAY_MS = 60_000

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
    if (!auth.refreshToken) return
    try {
      const res = await refreshAccessToken(auth.refreshToken)
      auth.setAccessToken(res.accessToken, res.expiresAt)
    } catch {
      auth.clear()
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
