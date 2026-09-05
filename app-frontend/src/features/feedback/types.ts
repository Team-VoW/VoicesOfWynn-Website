export interface Page<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
export interface QuestSummary {
  groupingKey: string
  questName: string
  unmatched: boolean
  unmatchedCount: number
  averageScore: number
  ratingCount: number
  commentCount: number
  latestSubmission: string
}
export interface FeedbackItem {
  questName: string
  unmatched: boolean
  score: number
  comment: string | null
  modVersion: string
  createdAt: string
}
export interface QuestDetail {
  distribution: { score: number; count: number }[]
  feedback: Page<FeedbackItem>
}
