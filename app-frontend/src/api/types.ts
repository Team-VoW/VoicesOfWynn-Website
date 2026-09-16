export type ReportStatus = 'unprocessed' | 'forwarded' | 'rejected' | 'accepted' | 'fixed'

export const REPORT_STATUSES: ReportStatus[] = [
  'unprocessed',
  'forwarded',
  'rejected',
  'accepted',
  'fixed',
]

export const REPORT_SORT_FIELDS = [
  'reportId',
  'npcName',
  'chatMessage',
  'status',
  'reportedTimes',
  'timeSubmitted',
] as const

export type ReportSortField = (typeof REPORT_SORT_FIELDS)[number]

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

export interface ImportVoicedLinesSkipped {
  noAudioFile: number
  blankLine: number
  tooLong: number
  duplicateInFile: number
  total: number
}

export interface ImportVoicedLinesResponse {
  totalEntries: number
  uniqueLines: number
  alreadyPresent: number
  rowsInserted: number
  skipped: ImportVoicedLinesSkipped
}

export type DailyUsageRange = '30' | '90' | '365' | 'all'

export interface DailyUsagePoint {
  date: string
  bootups: number
  rollingAverage7Day: number
}

export interface DailyUsageResponse {
  range: DailyUsageRange
  totalBootups: number
  averageBootupsPerDay: number
  peakDay: DailyUsagePoint | null
  previousPeriodChangePercent: number | null
  points: DailyUsagePoint[]
}

export interface AuthTokenResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export interface PasswordLoginRequest {
  username: string
  password: string
}

export interface PasswordLoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  forcePasswordChange: boolean
}

export interface AuthHandoffRequest {
  code: string
}

export interface ContentOption {
  id: number
  name: string
  voiceActorName?: string | null
}

export interface ContentOptionsResponse {
  quests: ContentOption[]
  npcs: ContentOption[]
  writers: ContentOption[]
  voiceActors: ContentOption[]
  soundEditors: ContentOption[]
}

export interface ContentSearchRequest {
  quest?: string
  npc?: string
  page: number
  pageSize: number
}

export interface ContentSearchNpc {
  npcId: number
  npcName: string
  npcDegeneratedName: string
  voiceActorId: number | null
  voiceActorName: string | null
  soundEditorId: number | null
  soundEditorName: string | null
  recordingCount: number
}

export interface ContentSearchQuest {
  questId: number
  questName: string
  questDegeneratedName: string
  writerId: number | null
  writerName: string | null
  scriptUrl: string | null
  npcs: ContentSearchNpc[]
}

export interface ContentSearchResponse {
  total: number
  page: number
  pageSize: number
  results: ContentSearchQuest[]
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

export interface ArchiveNpcRequest {
  createReplacement: boolean
}

export interface ArchiveNpcResponse {
  replacementNpcId: number | null
}

export interface UpdateContentNameRequest {
  name: string
}

export interface UpdateQuestWriterRequest {
  writerUserId?: number
}

export interface UpdateNpcVoiceActorRequest {
  voiceActorUserId?: number
}

export interface UpdateQuestNpcSoundEditorRequest {
  soundEditorUserId?: number
}

export interface LinkQuestNpcRequest {
  npcId: number
}

export interface AccountRole {
  id: number
  name: string
  color: string
  weight: number
}

export interface AccountSearchRequest {
  query?: string
  page: number
  pageSize: number
}

export interface AccountSearchResult {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
  socialSummary: string
  roleIds: number[]
}

export interface AccountSearchResponse {
  total: number
  page: number
  pageSize: number
  results: AccountSearchResult[]
}

export interface AccountDetails {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
  discordId: string | null
  email: string | null
  publicEmail: boolean
  discord: string | null
  youtube: string | null
  twitter: string | null
  castingCallClub: string | null
  bio: string | null
  lore: string | null
  forcePasswordChange: boolean
  systemAdmin: boolean
  roleIds: number[]
}

export interface UpdateAccountRequest {
  displayName: string
  password?: string
  discordId?: string
  email?: string
  publicEmail?: boolean | null
  discord?: string
  youtube?: string
  twitter?: string
  castingCallClub?: string
  bio?: string
  lore?: string
}

export interface SelfProfile {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
  email: string | null
  publicEmail: boolean
  discord: string | null
  youtube: string | null
  twitter: string | null
  castingCallClub: string | null
  bio: string | null
  lore: string | null
  forcePasswordChange: boolean
  passwordChangeRequiresCurrentPassword: boolean
  roles: AccountRole[]
}

export interface UpdateSelfProfileRequest {
  displayName: string
  email: string | null
  publicEmail: boolean
  discord: string | null
  youtube: string | null
  twitter: string | null
  castingCallClub: string | null
  bio: string | null
  lore: string | null
}

export interface SetSelfPasswordRequest {
  oldPassword: string | null
  newPassword: string
  confirmNewPassword: string
}

export interface UpdateAccountRolesRequest {
  roleIds: number[]
}

export interface ResetPasswordResponse {
  temporaryPassword: string
}

export interface CreateAccountRequest {
  displayName: string
  discordId?: string
  discord?: string
  castingCallClub?: string
}

export interface CreateAccountResponse {
  userId: number
  temporaryPassword: string
}

export interface UploadNpcRecordingResult {
  fileName: string
  code: number
  message: string
  description: string
  storedFileName: string | null
  conflict: RecordingConflict | null
}

export interface UploadNpcRecordingsResponse {
  results: UploadNpcRecordingResult[]
}

export interface RecordingConflict {
  recordingId: number
  questId: number
  questName: string
  npcId: number
  npcName: string
  line: number
  fileName: string
}

export interface NpcRecording {
  recordingId: number
  line: number
  fileName: string
  url: string
}

export interface ContributorRole {
  id: number
  name: string
  color: string
  weight: number
}

export interface ContributorSummary {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
  lore: string | null
  /** The role the contributor is credited under; the credits page groups by it. */
  topRole: ContributorRole
  roles: ContributorRole[]
}

export interface ContributorListResponse {
  total: number
  page: number
  pageSize: number
  results: ContributorSummary[]
}

export interface NpcQuestAppearance {
  questId: number
  questName: string
  questDegeneratedName: string
}

export interface VoicedNpc {
  npcId: number
  npcName: string
  imageUrl: string
  defaultImageUrl: string
  archived: boolean
  upvotes: number
  downvotes: number
  commentCount: number
  recordingCount: number
  quests: NpcQuestAppearance[]
}

export interface WrittenQuest {
  questId: number
  questName: string
  questDegeneratedName: string
  scriptUrl: string | null
}

export interface EditedNpc {
  npcId: number
  npcName: string
}

export interface EditedQuest {
  questId: number
  questName: string
  questDegeneratedName: string
  npcs: EditedNpc[]
}

export interface ContributorDetail {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
  lore: string | null
  /** Sanitized HTML, rendered as markup. */
  bio: string | null
  email: string | null
  discord: string | null
  youtube: string | null
  twitter: string | null
  castingCallClub: string | null
  roles: ContributorRole[]
  voicing: VoicedNpc[]
  scriptwriting: WrittenQuest[]
  soundEditing: EditedQuest[]
}

/** The caller's standing votes among a set of NPCs, keyed apart from the page that lists them. */
export interface NpcVotes {
  upvoted: number[]
  downvoted: number[]
}

export type ContributorVotes = NpcVotes

export interface MyNpcVote {
  myVote: VoteType | null
}

/** A contributor credited on a quest or NPC page. */
export interface ContentCredit {
  userId: number
  displayName: string
  avatarUrl: string
  defaultAvatarUrl: string
}

export interface QuestListItem {
  questId: number
  questName: string
  questDegeneratedName: string
  npcCount: number
  recordingCount: number
}

export interface QuestListResponse {
  quests: QuestListItem[]
}

/** One NPC in the mod-contents index. Shaped for the shared NPC card, plus its voice actor. */
export interface NpcListItem {
  npcId: number
  npcName: string
  imageUrl: string
  defaultImageUrl: string
  archived: boolean
  upvotes: number
  downvotes: number
  commentCount: number
  recordingCount: number
  voiceActor: ContentCredit | null
  quests: NpcQuestAppearance[]
}

export interface NpcListResponse {
  total: number
  page: number
  pageSize: number
  results: NpcListItem[]
}

export interface QuestNpc {
  npcId: number
  npcName: string
  imageUrl: string
  defaultImageUrl: string
  archived: boolean
  upvotes: number
  downvotes: number
  commentCount: number
  recordingCount: number
  voiceActor: ContentCredit | null
  soundEditor: ContentCredit | null
}

export interface QuestDetail {
  questId: number
  questName: string
  questDegeneratedName: string
  scriptUrl: string | null
  writer: ContentCredit | null
  npcs: QuestNpc[]
}

export interface NpcQuestCredit {
  questId: number
  questName: string
  questDegeneratedName: string
  soundEditor: ContentCredit | null
}

export interface NpcDetail {
  npcId: number
  npcName: string
  imageUrl: string
  defaultImageUrl: string
  archived: boolean
  upvotes: number
  downvotes: number
  commentCount: number
  recordingCount: number
  voiceActor: ContentCredit | null
  quests: NpcQuestCredit[]
}

export type VoteType = 'Up' | 'Down'

export interface NpcVoteResponse {
  upvotes: number
  downvotes: number
  myVote: VoteType | null
}

export interface NpcRecordingLine {
  recordingId: number
  line: number
  url: string
}

export interface NpcQuestRecordings {
  questId: number
  questName: string
  questDegeneratedName: string
  recordings: NpcRecordingLine[]
}

export interface NpcRecordingsResponse {
  quests: NpcQuestRecordings[]
}

export interface NpcComment {
  commentId: number
  verified: boolean
  authorName: string
  avatarUrl: string | null
  content: string
  /** Null for comments posted before the API recorded timestamps. */
  createdAt: string | null
  canDelete: boolean
}

export interface NpcCommentsResponse {
  comments: NpcComment[]
}

export interface PostNpcCommentRequest {
  name?: string | null
  email?: string | null
  content: string
}
