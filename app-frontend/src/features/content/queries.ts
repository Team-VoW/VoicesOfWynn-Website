import { computed, type ComputedRef, type Ref } from 'vue'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import {
  createNpc,
  createQuest,
  deleteQuest,
  getContentOptions,
  linkQuestNpc,
  searchContent,
  unlinkQuestNpc,
  updateNpc,
  updateNpcVoiceActor,
  updateQuest,
  updateQuestNpcSoundEditor,
  updateQuestWriter,
} from '@/api/content'
import type {
  ContentSearchRequest,
  CreateNpcRequest,
  CreateQuestRequest,
  LinkQuestNpcRequest,
  UpdateContentNameRequest,
  UpdateNpcVoiceActorRequest,
  UpdateQuestNpcSoundEditorRequest,
  UpdateQuestWriterRequest,
} from '@/api/types'

export function useContentOptions() {
  return useQuery({
    queryKey: ['content', 'options'],
    queryFn: ({ signal }) => getContentOptions(signal),
    staleTime: 60_000,
  })
}

export function useContentSearch(
  params: Ref<ContentSearchRequest> | ComputedRef<ContentSearchRequest>,
) {
  return useQuery({
    queryKey: computed(() => ['content', 'search', params.value] as const),
    queryFn: ({ signal }) => searchContent(params.value, signal),
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  })
}

export function useCreateQuest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateQuestRequest) => createQuest(request),
    onSuccess: () => {
      invalidateContent(queryClient)
    },
  })
}

export function useCreateNpc() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateNpcRequest) => createNpc(request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

function invalidateContent(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: ['content'] })
}

export function useUpdateQuest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ questId, request }: { questId: number; request: UpdateContentNameRequest }) =>
      updateQuest(questId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useDeleteQuest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (questId: number) => deleteQuest(questId),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useUpdateQuestWriter() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ questId, request }: { questId: number; request: UpdateQuestWriterRequest }) =>
      updateQuestWriter(questId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useUpdateNpc() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ npcId, request }: { npcId: number; request: UpdateContentNameRequest }) =>
      updateNpc(npcId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useUpdateNpcVoiceActor() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ npcId, request }: { npcId: number; request: UpdateNpcVoiceActorRequest }) =>
      updateNpcVoiceActor(npcId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useLinkQuestNpc() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ questId, request }: { questId: number; request: LinkQuestNpcRequest }) =>
      linkQuestNpc(questId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useUpdateQuestNpcSoundEditor() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      questId,
      npcId,
      request,
    }: {
      questId: number
      npcId: number
      request: UpdateQuestNpcSoundEditorRequest
    }) => updateQuestNpcSoundEditor(questId, npcId, request),
    onSuccess: () => invalidateContent(queryClient),
  })
}

export function useUnlinkQuestNpc() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ questId, npcId }: { questId: number; npcId: number }) =>
      unlinkQuestNpc(questId, npcId),
    onSuccess: () => invalidateContent(queryClient),
  })
}
