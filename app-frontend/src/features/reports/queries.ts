import { computed, type ComputedRef, type Ref } from 'vue'
import { keepPreviousData, useMutation, useQueryClient, useQuery } from '@tanstack/vue-query'
import { deleteReport, searchReports, updateReportStatus } from '@/api/reports'
import type { ReportSearchRequest, ReportStatus } from '@/api/types'

export function useReportsSearch(params: Ref<ReportSearchRequest> | ComputedRef<ReportSearchRequest>) {
  return useQuery({
    queryKey: computed(() => ['reports', 'search', params.value] as const),
    queryFn: ({ signal }) => searchReports(params.value, signal),
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  })
}

export function useUpdateReportStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ reportId, status }: { reportId: number; status: ReportStatus }) =>
      updateReportStatus(reportId, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reports', 'search'] })
    },
  })
}

export function useDeleteReport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reportId: number) => deleteReport(reportId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reports', 'search'] })
    },
  })
}
