import type { NpcQuestAppearance } from '@/api/types'

/**
 * The least an NPC card needs. The cast, quest and NPC pages each carry more than this, so the
 * card takes the common shape rather than one page's response type.
 */
export interface NpcCardNpc {
  npcId: number
  npcName: string
  imageUrl: string
  defaultImageUrl: string
  archived: boolean
  upvotes: number
  downvotes: number
  commentCount: number
  recordingCount: number
  quests?: NpcQuestAppearance[]
}

/** Enough to title a comments dialog and address the API. */
export interface NpcIdentity {
  npcId: number
  npcName: string
}
