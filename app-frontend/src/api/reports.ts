import { apiFetch } from './client'
import type {
  ImportVoicedLinesResponse,
  ReportSearchRequest,
  ReportSearchResponse,
  ReportStatus,
} from './types'

export function searchReports(
  params: ReportSearchRequest,
  signal?: AbortSignal,
): Promise<ReportSearchResponse> {
  return apiFetch<ReportSearchResponse>('/admin/reports/search', {
    query: {
      npc: params.npc,
      content: params.content,
      status: params.status,
      sortBy: params.sortBy,
      sortDir: params.sortDir,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  })
}

export function updateReportStatus(reportId: number, status: ReportStatus): Promise<void> {
  return apiFetch<void>(`/admin/reports/${reportId}/status`, {
    method: 'PATCH',
    body: { status },
  })
}

export function deleteReport(reportId: number): Promise<void> {
  return apiFetch<void>(`/admin/reports/${reportId}`, {
    method: 'DELETE',
  })
}

export function importVoicedLines(file: File): Promise<ImportVoicedLinesResponse> {
  const form = new FormData()
  form.append('file', file)

  return apiFetch<ImportVoicedLinesResponse>('/admin/reports/import/voiced', {
    method: 'POST',
    body: form,
  })
}
