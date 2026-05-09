import { useLocalStorage } from '@vueuse/core'
import type { ReportSortField } from '@/api/types'

type Visibility = Record<ReportSortField, boolean>

const STORAGE_KEY = 'vow.reports.columnVisibility'

const DEFAULTS: Visibility = {
  npcName: true,
  chatMessage: true,
  status: false,
  reportedTimes: true,
  timeSubmitted: false,
}

export const REPORT_COLUMN_LABELS: Record<ReportSortField, string> = {
  npcName: 'NPC',
  chatMessage: 'Message',
  status: 'Status',
  reportedTimes: 'Reports',
  timeSubmitted: 'Submitted',
}

export function useReportColumnVisibility() {
  const visibility = useLocalStorage<Visibility>(STORAGE_KEY, DEFAULTS, {
    mergeDefaults: true,
  })

  function toggle(key: ReportSortField, value: boolean) {
    visibility.value[key] = value
  }

  return { visibility, toggle, columnLabels: REPORT_COLUMN_LABELS }
}
