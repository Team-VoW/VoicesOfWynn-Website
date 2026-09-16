import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  aggregateUsage,
  createBroadcast,
  createFunFact,
  deleteBroadcast,
  deleteFunFact,
  getBroadcasts,
  getFunFacts,
  getModRelease,
  updateBroadcast,
  updateFunFact,
  updateModRelease,
} from '@/api/admin'
import type { SaveBroadcastRequest, SaveFunFactRequest, UpdateModReleaseRequest } from '@/api/types'

export function useAggregateUsage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: aggregateUsage,
    onSuccess: () => {
      // The daily usage chart reads the table this just wrote to.
      queryClient.invalidateQueries({ queryKey: ['analytics', 'daily'] })
    },
  })
}

export function useModRelease() {
  return useQuery({
    queryKey: ['admin', 'mod', 'release'] as const,
    queryFn: ({ signal }) => getModRelease(signal),
  })
}

export function useUpdateModRelease() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateModReleaseRequest) => updateModRelease(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'mod', 'release'] }),
  })
}

export function useBroadcasts() {
  return useQuery({
    queryKey: ['admin', 'mod', 'broadcasts'] as const,
    queryFn: ({ signal }) => getBroadcasts(signal),
  })
}

export function useSaveBroadcast() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: number | null; body: SaveBroadcastRequest }) =>
      id === null ? createBroadcast(body).then(() => undefined) : updateBroadcast(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'mod', 'broadcasts'] }),
  })
}

export function useDeleteBroadcast() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteBroadcast(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'mod', 'broadcasts'] }),
  })
}

export function useFunFacts() {
  return useQuery({
    queryKey: ['admin', 'mod', 'fun-facts'] as const,
    queryFn: ({ signal }) => getFunFacts(signal),
  })
}

export function useSaveFunFact() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, body }: { id: number | null; body: SaveFunFactRequest }) =>
      id === null ? createFunFact(body).then(() => undefined) : updateFunFact(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'mod', 'fun-facts'] }),
  })
}

export function useDeleteFunFact() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => deleteFunFact(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'mod', 'fun-facts'] }),
  })
}
