import { apiFetch } from './client'
import type {
  ContentOptionsResponse,
  ContentSearchRequest,
  ContentSearchResponse,
  CreateContentResponse,
  CreateNpcRequest,
  CreateQuestRequest,
  LinkQuestNpcRequest,
  UpdateContentNameRequest,
  UpdateNpcVoiceActorRequest,
} from './types'

export function getContentOptions(signal?: AbortSignal): Promise<ContentOptionsResponse> {
  return apiFetch<ContentOptionsResponse>('/admin/content/options', { signal })
}

export function searchContent(
  params: ContentSearchRequest,
  signal?: AbortSignal,
): Promise<ContentSearchResponse> {
  return apiFetch<ContentSearchResponse>('/admin/content/search', {
    query: {
      quest: params.quest,
      npc: params.npc,
      page: params.page,
      pageSize: params.pageSize,
    },
    signal,
  })
}

export function createQuest(request: CreateQuestRequest): Promise<CreateContentResponse> {
  return apiFetch<CreateContentResponse>('/admin/content/quests', {
    method: 'POST',
    body: request,
  })
}

export function createNpc(request: CreateNpcRequest): Promise<CreateContentResponse> {
  return apiFetch<CreateContentResponse>('/admin/content/npcs', {
    method: 'POST',
    body: request,
  })
}

export function updateQuest(questId: number, request: UpdateContentNameRequest): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}`, {
    method: 'PATCH',
    body: request,
  })
}

export function deleteQuest(questId: number): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}`, {
    method: 'DELETE',
  })
}

export function updateNpc(npcId: number, request: UpdateContentNameRequest): Promise<void> {
  return apiFetch<void>(`/admin/content/npcs/${npcId}`, {
    method: 'PATCH',
    body: request,
  })
}

export function updateNpcVoiceActor(
  npcId: number,
  request: UpdateNpcVoiceActorRequest,
): Promise<void> {
  return apiFetch<void>(`/admin/content/npcs/${npcId}/voice-actor`, {
    method: 'PATCH',
    body: request,
  })
}

export function linkQuestNpc(questId: number, request: LinkQuestNpcRequest): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}/npcs`, {
    method: 'POST',
    body: request,
  })
}

export function unlinkQuestNpc(questId: number, npcId: number): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}/npcs/${npcId}`, {
    method: 'DELETE',
  })
}
