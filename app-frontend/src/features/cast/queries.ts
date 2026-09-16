import { computed, type ComputedRef, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { getContributor, getContributorVotes } from '@/api/contributors'

export function useContributor(userId: Ref<number> | ComputedRef<number>) {
  return useQuery({
    queryKey: computed(() => ['contributor', userId.value] as const),
    queryFn: ({ signal }) => getContributor(userId.value, signal),
    staleTime: 60_000,
  })
}

/**
 * The caller's own votes, kept apart from the profile so that response stays identical for every
 * visitor. A failure here only costs the highlight, so the page never waits on it.
 */
export function useContributorVotes(userId: Ref<number> | ComputedRef<number>) {
  return useQuery({
    queryKey: computed(() => ['contributor', userId.value, 'my-votes'] as const),
    queryFn: ({ signal }) => getContributorVotes(userId.value, signal),
    staleTime: 60_000,
    retry: false,
  })
}
