import { apiFetch } from './client'
import type { DailyUsageRange, DailyUsageResponse } from './types'

export function getDailyUsage(
  range: DailyUsageRange,
  signal?: AbortSignal,
): Promise<DailyUsageResponse> {
  return apiFetch<DailyUsageResponse>('/admin/analytics/daily', {
    query: { range },
    signal,
  })
}
