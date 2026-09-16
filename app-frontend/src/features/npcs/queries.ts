import { computed, type ComputedRef, type Ref } from 'vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
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
    onSuccess: (_data, variables) =>
      queryClient.invalidateQueries({ queryKey: ['npc', variables.npcId, 'comments'] }),
  })
}

export function useDeleteNpcComment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ npcId, commentId }: { npcId: number; commentId: number }) =>
      deleteNpcComment(npcId, commentId),
    onSuccess: (_data, variables) =>
      queryClient.invalidateQueries({ queryKey: ['npc', variables.npcId, 'comments'] }),
  })
}
