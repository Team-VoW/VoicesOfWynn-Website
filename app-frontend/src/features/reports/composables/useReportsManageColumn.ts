import { computed } from 'vue'
import { useLocalStorage } from '@vueuse/core'
import { useAuthStore } from '@/stores/auth'
import { Capabilities } from '@/lib/capabilities'

const STORAGE_KEY = 'vow.reports.manageColumnVisible'

export function useReportsManageColumn() {
  const auth = useAuthStore()
  const canManage = computed(() => auth.hasCapability(Capabilities.ReportsManage))

  const stored = useLocalStorage<boolean>(STORAGE_KEY, true)
  const visible = computed(() => canManage.value && stored.value)

  function toggle(value: boolean) {
    stored.value = value
  }

  return { canManage, visible, stored, toggle }
}
