import { computed, type ComputedRef, type Ref } from 'vue'
import { keepPreviousData, useQuery } from '@tanstack/vue-query'
import { searchReports } from '@/api/reports'
import type { ReportSearchRequest } from '@/api/types'

export function useReportsSearch(params: Ref<ReportSearchRequest> | ComputedRef<ReportSearchRequest>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'search', params.value] as const),
    queryFn: ({ signal }) => searchReports(params.value, signal),
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  })
}
