import { computed, type ComputedRef, type Ref } from 'vue'
import { useInfiniteQuery, useQueries, useQuery } from '@tanstack/vue-query'
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
 *
 * Asked one page of ids at a time rather than as a single growing list. The server caps a lookup
 * at 200 ids, so re-sending everything the infinite scroll has accumulated would quietly stop
 * highlighting anything past that point, and the query string would keep growing until it no
 * longer fit in a request line. Chunking on the page size also leaves every key but the newest
 * one unchanged, so a page's votes are fetched once.
 */
export function useNpcListVotes(npcIds: Ref<number[]> | ComputedRef<number[]>) {
  return useQueries({
    queries: computed(() =>
      chunk(npcIds.value, NPCS_PAGE_SIZE).map((ids) => ({
        queryKey: ['npcs', 'my-votes', ids] as const,
        queryFn: ({ signal }: { signal: AbortSignal }) => getNpcListVotes(ids, signal),
        staleTime: 60_000,
        retry: false,
      })),
    ),
    // Pages that have not answered yet simply contribute nothing, so the highlights already on
    // screen stay put while the newest page is in flight.
    combine: (results) => ({
      upvoted: results.flatMap((result) => result.data?.upvoted ?? []),
      downvoted: results.flatMap((result) => result.data?.downvoted ?? []),
    }),
  })
}

function chunk(ids: readonly number[], size: number) {
  const chunks: number[][] = []
  for (let start = 0; start < ids.length; start += size) {
    chunks.push(ids.slice(start, start + size))
  }
  return chunks
}
