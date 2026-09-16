import { computed, type ComputedRef, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { getMyNpcVote, getNpc, getQuest, getQuestVotes, listQuests } from '@/api/contents'

export function useQuests() {
  return useQuery({
    queryKey: ['quests'] as const,
    queryFn: ({ signal }) => listQuests(signal),
    // The index changes only when a quest is added, and the whole page is filtered from it.
    staleTime: 5 * 60_000,
  })
}

export function useQuest(name: Ref<string> | ComputedRef<string>) {
  return useQuery({
    queryKey: computed(() => ['quest', name.value] as const),
    queryFn: ({ signal }) => getQuest(name.value, signal),
    staleTime: 60_000,
  })
}

/**
 * The caller's own votes, kept apart from the quest so that response stays identical for every
 * visitor. A failure here only costs the highlight, so the page never waits on it.
 */
export function useQuestVotes(name: Ref<string> | ComputedRef<string>) {
  return useQuery({
    queryKey: computed(() => ['quest', name.value, 'my-votes'] as const),
    queryFn: ({ signal }) => getQuestVotes(name.value, signal),
    staleTime: 60_000,
    retry: false,
  })
}

export function useNpc(npcId: Ref<number> | ComputedRef<number>) {
  return useQuery({
    queryKey: computed(() => ['npc', npcId.value, 'detail'] as const),
    queryFn: ({ signal }) => getNpc(npcId.value, signal),
    staleTime: 60_000,
  })
}

/** Split from the NPC for the same reason as the quest votes above. */
export function useMyNpcVote(npcId: Ref<number> | ComputedRef<number>) {
  return useQuery({
    queryKey: computed(() => ['npc', npcId.value, 'my-vote'] as const),
    queryFn: ({ signal }) => getMyNpcVote(npcId.value, signal),
    staleTime: 60_000,
    retry: false,
  })
}
