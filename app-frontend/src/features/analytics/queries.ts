import { computed, type ComputedRef, type Ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { getDailyUsage } from '@/api/analytics'
import type { DailyUsageRange } from '@/api/types'

export function useDailyUsage(range: Ref<DailyUsageRange> | ComputedRef<DailyUsageRange>) {
  return useQuery({
    queryKey: computed(() => ['analytics', 'daily', range.value] as const),
    queryFn: ({ signal }) => getDailyUsage(range.value, signal),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  })
}
