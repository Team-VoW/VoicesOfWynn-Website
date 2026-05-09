import { apiFetch } from './client'
import type {
  ContentOptionsResponse,
  ContentSearchRequest,
  ContentSearchResponse,
  CreateContentResponse,
  CreateNpcRequest,
  CreateQuestRequest,
  LinkQuestNpcRequest,
  NpcRecording,
  UpdateContentNameRequest,
  UpdateNpcVoiceActorRequest,
  UpdateQuestNpcSoundEditorRequest,
  UpdateQuestWriterRequest,
  UploadNpcRecordingsResponse,
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

export function updateQuestWriter(
  questId: number,
  request: UpdateQuestWriterRequest,
): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}/writer`, {
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

export function updateQuestNpcSoundEditor(
  questId: number,
  npcId: number,
  request: UpdateQuestNpcSoundEditorRequest,
): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}/npcs/${npcId}/sound-editor`, {
    method: 'PATCH',
    body: request,
  })
}

export function unlinkQuestNpc(questId: number, npcId: number): Promise<void> {
  return apiFetch<void>(`/admin/content/quests/${questId}/npcs/${npcId}`, {
    method: 'DELETE',
  })
}

export function getNpcRecordings(
  questId: number,
  npcId: number,
  signal?: AbortSignal,
): Promise<NpcRecording[]> {
  return apiFetch<NpcRecording[]>(`/admin/content/quests/${questId}/npcs/${npcId}/recordings`, {
    signal,
  })
}

export function deleteNpcRecording(
  questId: number,
  npcId: number,
  recordingId: number,
): Promise<void> {
  return apiFetch<void>(
    `/admin/content/quests/${questId}/npcs/${npcId}/recordings/${recordingId}`,
    {
      method: 'DELETE',
    },
  )
}

export function uploadQuestScript(questId: number, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  return apiFetch<void>(`/admin/content/quests/${questId}/script`, {
    method: 'PUT',
    body: form,
  })
}

export function uploadNpcImage(npcId: number, file: Blob, fileName = 'image.webp'): Promise<void> {
  const form = new FormData()
  form.append('file', file, fileName)
  return apiFetch<void>(`/admin/content/npcs/${npcId}/image`, {
    method: 'PUT',
    body: form,
  })
}

export function uploadNpcRecordings(
  questId: number,
  npcId: number,
  recordings: File[],
  overwrite: boolean,
): Promise<UploadNpcRecordingsResponse> {
  const form = new FormData()
  for (const recording of recordings) {
    form.append('recordings', recording)
  }
  form.append('overwrite', String(overwrite))

  return apiFetch<UploadNpcRecordingsResponse>(
    `/admin/content/quests/${questId}/npcs/${npcId}/recordings`,
    {
      method: 'PUT',
      body: form,
    },
  )
}
