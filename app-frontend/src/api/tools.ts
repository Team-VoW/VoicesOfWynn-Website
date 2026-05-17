import { apiFetch } from './client'

export interface AudioAnalysisItem {
  fileName: string
  success: boolean
  integratedLufs: number | null
  leadingSilenceSeconds: number | null
  trailingSilenceSeconds: number | null
  error: string | null
}

export interface AudioAnalysisBatchResponse {
  results: AudioAnalysisItem[]
}

export function analyzeAudio(files: File[], signal?: AbortSignal): Promise<AudioAnalysisBatchResponse> {
  const form = new FormData()
  for (const f of files) form.append('files', f, f.name)
  return apiFetch<AudioAnalysisBatchResponse>('/admin/tools/audio-analysis', {
    method: 'POST',
    body: form,
    signal,
  })
}
