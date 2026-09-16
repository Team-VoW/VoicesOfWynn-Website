import { apiFetch } from './client'
import type {
  NpcCommentsResponse,
  NpcRecordingsResponse,
  NpcVoteResponse,
  PostNpcCommentRequest,
  VoteType,
} from './types'

export function getNpcRecordings(
  npcId: number,
  signal?: AbortSignal,
): Promise<NpcRecordingsResponse> {
  return apiFetch<NpcRecordingsResponse>(`/npcs/${npcId}/recordings`, { signal })
}

export function setNpcVote(npcId: number, vote: VoteType): Promise<NpcVoteResponse> {
  return apiFetch<NpcVoteResponse>(`/npcs/${npcId}/vote`, { method: 'PUT', body: { vote } })
}

export function clearNpcVote(npcId: number): Promise<NpcVoteResponse> {
  return apiFetch<NpcVoteResponse>(`/npcs/${npcId}/vote`, { method: 'DELETE' })
}

export function getNpcComments(npcId: number, signal?: AbortSignal): Promise<NpcCommentsResponse> {
  return apiFetch<NpcCommentsResponse>(`/npcs/${npcId}/comments`, { signal })
}

export function postNpcComment(
  npcId: number,
  request: PostNpcCommentRequest,
): Promise<NpcCommentsResponse['comments'][number]> {
  return apiFetch(`/npcs/${npcId}/comments`, { method: 'POST', body: request })
}

export function deleteNpcComment(npcId: number, commentId: number): Promise<void> {
  return apiFetch<void>(`/npcs/${npcId}/comments/${commentId}`, { method: 'DELETE' })
}
