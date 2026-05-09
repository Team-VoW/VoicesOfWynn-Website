import { apiFetch } from './client'
import type {
  ContentOptionsResponse,
  CreateContentResponse,
  CreateNpcRequest,
  CreateQuestRequest,
} from './types'

export function getContentOptions(signal?: AbortSignal): Promise<ContentOptionsResponse> {
  return apiFetch<ContentOptionsResponse>('/admin/content/options', { signal })
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
