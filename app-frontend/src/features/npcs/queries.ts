import { computed, type ComputedRef, type Ref } from 'vue'
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/vue-query'
import {
  clearNpcVote,
  deleteNpcComment,
  getNpcComments,
  getNpcRecordings,
  postNpcComment,
  setNpcVote,
} from '@/api/npcs'
import type { PostNpcCommentRequest, VoteType } from '@/api/types'

export function useNpcRecordings(
  npcId: Ref<number> | ComputedRef<number>,
  enabled: Ref<boolean> | ComputedRef<boolean>,
) {
  return useQuery({
    queryKey: computed(() => ['npc', npcId.value, 'recordings'] as const),
    queryFn: ({ signal }) => getNpcRecordings(npcId.value, signal),
    enabled,
    // Recordings never change while a page is open, so they are fetched once per NPC.
    staleTime: Infinity,
  })
}

export function useNpcVote() {
  return useMutation({
    mutationFn: ({ npcId, vote }: { npcId: number; vote: VoteType | null }) =>
      vote === null ? clearNpcVote(npcId) : setNpcVote(npcId, vote),
  })
}

export function useNpcComments(
  npcId: Ref<number | null> | ComputedRef<number | null>,
  enabled: Ref<boolean> | ComputedRef<boolean>,
) {
  return useQuery({
    queryKey: computed(() => ['npc', npcId.value, 'comments'] as const),
    queryFn: ({ signal }) => getNpcComments(npcId.value!, signal),
    enabled,
  })
}

export function usePostNpcComment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ npcId, request }: { npcId: number; request: PostNpcCommentRequest }) =>
      postNpcComment(npcId, request),
    onSuccess: (_data, variables) => {
      shiftCommentCount(queryClient, variables.npcId, 1)
      return queryClient.invalidateQueries({ queryKey: ['npc', variables.npcId, 'comments'] })
    },
  })
}

export function useDeleteNpcComment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ npcId, commentId }: { npcId: number; commentId: number }) =>
      deleteNpcComment(npcId, commentId),
    onSuccess: (_data, variables) => {
      shiftCommentCount(queryClient, variables.npcId, -1)
      return queryClient.invalidateQueries({ queryKey: ['npc', variables.npcId, 'comments'] })
    },
  })
}

/**
 * The comment badge is drawn from the NPC index, the quest cast, the NPC profile and the
 * contributor page rather than from the thread itself, so a post or a delete has to reach into
 * whichever of those the visitor has cached. Invalidating them instead would pull every loaded
 * page of the infinite index back down for the sake of one number.
 */
function shiftCommentCount(queryClient: QueryClient, npcId: number, by: number) {
  queryClient.setQueriesData({ queryKey: [] }, (data: unknown) => withShiftedCount(data, npcId, by))
}

/** Rewrites only the branches that actually changed, so untouched queries keep their identity. */
function withShiftedCount(value: unknown, npcId: number, by: number): unknown {
  if (Array.isArray(value)) {
    let changed = false
    const next = value.map((item) => {
      const updated = withShiftedCount(item, npcId, by)
      changed ||= updated !== item
      return updated
    })
    return changed ? next : value
  }

  if (value === null || typeof value !== 'object') return value

  const record = value as Record<string, unknown>
  // Every shape that renders the badge carries both fields on the same object.
  if (record.npcId === npcId && typeof record.commentCount === 'number') {
    return { ...record, commentCount: Math.max(0, record.commentCount + by) }
  }

  let changed = false
  const next: Record<string, unknown> = {}
  for (const [key, item] of Object.entries(record)) {
    const updated = withShiftedCount(item, npcId, by)
    changed ||= updated !== item
    next[key] = updated
  }
  return changed ? next : value
}
