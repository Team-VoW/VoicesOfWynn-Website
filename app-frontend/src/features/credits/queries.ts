import { useInfiniteQuery } from '@tanstack/vue-query'
import { listContributors } from '@/api/contributors'

export const CONTRIBUTORS_PAGE_SIZE = 24

export function useContributors() {
  return useInfiniteQuery({
    queryKey: ['contributors'] as const,
    queryFn: ({ pageParam, signal }) => listContributors(pageParam, CONTRIBUTORS_PAGE_SIZE, signal),
    initialPageParam: 1,
    getNextPageParam: (lastPage, pages) => {
      const loaded = pages.reduce((count, page) => count + page.results.length, 0)
      // An empty page also ends the scroll, so a miscounted total cannot loop forever.
      if (lastPage.results.length === 0 || loaded >= lastPage.total) return undefined
      return lastPage.page + 1
    },
    staleTime: 5 * 60_000,
  })
}
