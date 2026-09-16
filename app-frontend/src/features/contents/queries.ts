import { computed, type ComputedRef, type Ref } from 'vue'
import { useInfiniteQuery, useQuery } from '@tanstack/vue-query'
import {
  getMyNpcVote,
  getNpc,
  getNpcListVotes,
  getQuest,
  getQuestVotes,
  listNpcs,
  listQuests,
} from '@/api/contents'

export const NPCS_PAGE_SIZE = 24

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

/**
 * The NPC index, scrolled a page at a time. The search is the server's business here rather than
 * the client's, as it is for quests: only a page of characters is ever loaded.
 */
export function useNpcs(search: Ref<string> | ComputedRef<string>) {
  return useInfiniteQuery({
    queryKey: computed(() => ['npcs', search.value] as const),
    queryFn: ({ pageParam, signal }) => listNpcs(search.value, pageParam, NPCS_PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((count, page) => count + page.results.length, 0)
      // An empty page also ends the scroll, so a miscounted total cannot loop forever.
      if (lastPage.results.length === 0 || loaded >= lastPage.total) return undefined
      return lastPage.page + 1
    },
    // Typing rewrites the key, and without this the list would blink back to skeletons on every
    // term: the characters already on screen stay until the narrower ones arrive.
    placeholderData: (previous) => previous,
    staleTime: 5 * 60_000,
  })
}

/**
 * The caller's votes on the NPCs loaded so far, kept apart from the index so that response stays
 * identical for every visitor. A failure here only costs the highlight.
 */
export function useNpcListVotes(npcIds: Ref<number[]> | ComputedRef<number[]>) {
  return useQuery({
    queryKey: computed(() => ['npcs', 'my-votes', npcIds.value] as const),
    queryFn: ({ signal }) => getNpcListVotes(npcIds.value, signal),
    enabled: computed(() => npcIds.value.length > 0),
    // Re-asked as the list grows, so the previous answer stands while the next one is in flight.
    placeholderData: (previous) => previous,
    staleTime: 60_000,
    retry: false,
  })
}
