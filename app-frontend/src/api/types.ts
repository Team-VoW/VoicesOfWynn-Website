export type ReportStatus =
  | 'unprocessed'
  | 'forwarded'
  | 'rejected'
  | 'accepted'
  | 'fixed'

export const REPORT_STATUSES: ReportStatus[] = [
  'unprocessed',
  'forwarded',
  'rejected',
  'accepted',
  'fixed',
]

export interface ReportSearchRequest {
  npc?: string
  content?: string
  status?: ReportStatus
  page: number
  pageSize: number
}

export interface ReportSearchResult {
  reportId: number
  npcName: string | null
  chatMessage: string
  status: ReportStatus
  reportedTimes: number
  timeSubmitted: string
}

export interface ReportSearchResponse {
  total: number
  page: number
  results: ReportSearchResult[]
}

export interface AuthTokenResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface AuthHandoffRequest {
  code: string
}

export interface RefreshTokenResponse {
  accessToken: string
  expiresAt: string
}
