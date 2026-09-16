import { apiFetch } from './client'
import type { MyNpcVote, NpcDetail, NpcVotes, QuestDetail, QuestListResponse } from './types'

export function listQuests(signal?: AbortSignal): Promise<QuestListResponse> {
  return apiFetch<QuestListResponse>('/quests', { signal })
}

export function getQuest(degeneratedName: string, signal?: AbortSignal): Promise<QuestDetail> {
  return apiFetch<QuestDetail>(`/quests/${encodeURIComponent(degeneratedName)}`, { signal })
}

export function getQuestVotes(degeneratedName: string, signal?: AbortSignal): Promise<NpcVotes> {
  return apiFetch<NpcVotes>(`/quests/${encodeURIComponent(degeneratedName)}/my-votes`, { signal })
}

export function getNpc(npcId: number, signal?: AbortSignal): Promise<NpcDetail> {
  return apiFetch<NpcDetail>(`/npcs/${npcId}`, { signal })
}

export function getMyNpcVote(npcId: number, signal?: AbortSignal): Promise<MyNpcVote> {
  return apiFetch<MyNpcVote>(`/npcs/${npcId}/my-vote`, { signal })
}
