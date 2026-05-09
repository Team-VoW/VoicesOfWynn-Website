import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { createNpc, createQuest, getContentOptions } from '@/api/content'
import type { CreateNpcRequest, CreateQuestRequest } from '@/api/types'

export function useContentOptions() {
  return useQuery({
    queryKey: ['content', 'options'],
    queryFn: ({ signal }) => getContentOptions(signal),
    staleTime: 60_000,
  })
}

export function useCreateQuest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateQuestRequest) => createQuest(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['content', 'options'] })
    },
  })
}

export function useCreateNpc() {
  return useMutation({
    mutationFn: (request: CreateNpcRequest) => createNpc(request),
  })
}
