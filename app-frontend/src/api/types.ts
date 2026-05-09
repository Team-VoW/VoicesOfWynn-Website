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

export const REPORT_SORT_FIELDS = [
  'npcName',
  'chatMessage',
  'status',
  'reportedTimes',
  'timeSubmitted',
] as const

export type ReportSortField = typeof REPORT_SORT_FIELDS[number]

export type SortDirection = 'asc' | 'desc'

export interface ReportSearchRequest {
  npc?: string
  content?: string
  status?: ReportStatus
  sortBy?: ReportSortField
  sortDir?: SortDirection
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

export interface ContentOption {
  id: number
  name: string
}

export interface ContentOptionsResponse {
  quests: ContentOption[]
  writers: ContentOption[]
  voiceActors: ContentOption[]
  soundEditors: ContentOption[]
}

export interface CreateQuestRequest {
  name: string
  writerUserId?: number
}

export interface CreateNpcQuestAssignmentRequest {
  questId: number
  soundEditorUserId?: number
}

export interface CreateNpcRequest {
  name: string
  voiceActorUserId?: number
  questAssignments: CreateNpcQuestAssignmentRequest[]
}

export interface CreateContentResponse {
  id: number
}
