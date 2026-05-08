import { apiFetch } from './client'
import type { ReportSearchRequest, ReportSearchResponse } from './types'

export function searchReports(
  params: ReportSearchRequest,
  signal?: AbortSignal,
): Promise<ReportSearchResponse> {
  return apiFetch<ReportSearchResponse>('/admin/reports/search', {
    query: {
      npc: params.npc,
      content: params.content,
      status: params.status,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  })
}
