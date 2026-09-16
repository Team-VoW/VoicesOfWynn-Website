import { apiFetch } from './client'
import type {
  MyNpcVote,
  NpcDetail,
  NpcListResponse,
  NpcVotes,
  QuestDetail,
  QuestListResponse,
} from './types'

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

/**
 * One page of the NPC index. Unlike the quest index this is paged and searched by the server,
 * there being far more NPCs than the page could sensibly hold at once.
 */
export function listNpcs(
  search: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<NpcListResponse> {
  return apiFetch<NpcListResponse>('/npcs', {
    query: { search: search || undefined, page, pageSize },
    signal,
  })
}

/** The caller's standing votes among the NPCs the index has loaded so far. */
export function getNpcListVotes(
  npcIds: readonly number[],
  signal?: AbortSignal,
): Promise<NpcVotes> {
  return apiFetch<NpcVotes>('/npcs/my-votes', { query: { npcIds }, signal })
}
