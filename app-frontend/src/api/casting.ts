import { apiFetch } from './client'
import type {
  AdminCastingRoundDetail,
  AdminCastingRoundListResponse,
  CastingAuditionList,
  CastingMyPicksResponse,
  CastingReview,
  CastingRoundDetail,
  CastingRoundListResponse,
  CastingRoundStatus,
  SaveCastingCharacterRequest,
  SaveCastingRoundRequest,
} from './types'

// Voting

export function getOpenCastingRounds(signal?: AbortSignal): Promise<CastingRoundListResponse> {
  return apiFetch<CastingRoundListResponse>('/casting/rounds', { signal })
}

export function getCastingRound(roundId: number, signal?: AbortSignal): Promise<CastingRoundDetail> {
  return apiFetch<CastingRoundDetail>(`/casting/rounds/${roundId}`, { signal })
}

export function getMyCastingPicks(roundId: number, signal?: AbortSignal): Promise<CastingMyPicksResponse> {
  return apiFetch<CastingMyPicksResponse>(`/casting/rounds/${roundId}/my-votes`, { signal })
}

export function getCastingAuditions(characterId: number, signal?: AbortSignal): Promise<CastingAuditionList> {
  return apiFetch<CastingAuditionList>(`/casting/characters/${characterId}/auditions`, { signal })
}

export function castVote(auditionId: number, comment: string | null): Promise<void> {
  return apiFetch<void>(`/casting/auditions/${auditionId}/vote`, { method: 'PUT', body: { comment } })
}

export function removeVote(auditionId: number): Promise<void> {
  return apiFetch<void>(`/casting/auditions/${auditionId}/vote`, { method: 'DELETE' })
}

export function setCastingComment(auditionId: number, comment: string): Promise<void> {
  return apiFetch<void>(`/casting/auditions/${auditionId}/comment`, { method: 'PUT', body: { comment } })
}

export function deleteCastingComment(auditionId: number): Promise<void> {
  return apiFetch<void>(`/casting/auditions/${auditionId}/comment`, { method: 'DELETE' })
}

export function clearCharacterVotes(characterId: number): Promise<void> {
  return apiFetch<void>(`/casting/characters/${characterId}/votes`, { method: 'DELETE' })
}

export function setCharacterDone(characterId: number, done: boolean): Promise<void> {
  return apiFetch<void>(`/casting/characters/${characterId}/done`, { method: done ? 'PUT' : 'DELETE' })
}

// Management

export function getAdminCastingRounds(
  includeArchived: boolean,
  signal?: AbortSignal,
): Promise<AdminCastingRoundListResponse> {
  return apiFetch<AdminCastingRoundListResponse>('/admin/casting/rounds', {
    query: { includeArchived },
    signal,
  })
}

export function getAdminCastingRound(roundId: number, signal?: AbortSignal): Promise<AdminCastingRoundDetail> {
  return apiFetch<AdminCastingRoundDetail>(`/admin/casting/rounds/${roundId}`, { signal })
}

export function createCastingRound(body: SaveCastingRoundRequest): Promise<{ id: number }> {
  return apiFetch<{ id: number }>('/admin/casting/rounds', { method: 'POST', body })
}

export function updateCastingRound(roundId: number, body: SaveCastingRoundRequest): Promise<void> {
  return apiFetch<void>(`/admin/casting/rounds/${roundId}`, { method: 'PUT', body })
}

export function setCastingRoundStatus(roundId: number, status: CastingRoundStatus): Promise<void> {
  return apiFetch<void>(`/admin/casting/rounds/${roundId}/status`, { method: 'POST', body: { status } })
}

export function deleteCastingRound(roundId: number): Promise<void> {
  return apiFetch<void>(`/admin/casting/rounds/${roundId}`, { method: 'DELETE' })
}

export function importCccCasting(roundId: number, url: string): Promise<{ queued: boolean }> {
  return apiFetch<{ queued: boolean }>(`/admin/casting/rounds/${roundId}/import/ccc`, {
    method: 'POST',
    body: { url },
  })
}

export function getCastingReview(roundId: number, signal?: AbortSignal): Promise<CastingReview> {
  return apiFetch<CastingReview>(`/admin/casting/rounds/${roundId}/review`, { signal })
}

export function createCastingCharacter(roundId: number, body: SaveCastingCharacterRequest): Promise<{ id: number }> {
  return apiFetch<{ id: number }>(`/admin/casting/rounds/${roundId}/characters`, { method: 'POST', body })
}

export function updateCastingCharacter(characterId: number, body: SaveCastingCharacterRequest): Promise<void> {
  return apiFetch<void>(`/admin/casting/characters/${characterId}`, { method: 'PUT', body })
}

export function deleteCastingCharacter(characterId: number): Promise<void> {
  return apiFetch<void>(`/admin/casting/characters/${characterId}`, { method: 'DELETE' })
}

export function setCastingWinner(characterId: number, auditionId: number | null): Promise<void> {
  return apiFetch<void>(`/admin/casting/characters/${characterId}/winner`, {
    method: 'PUT',
    body: { auditionId },
  })
}

export function uploadCastingAudition(
  characterId: number,
  auditioneeName: string,
  file: File,
): Promise<{ id: number }> {
  const form = new FormData()
  form.append('auditioneeName', auditioneeName)
  form.append('file', file)
  return apiFetch<{ id: number }>(`/admin/casting/characters/${characterId}/auditions`, {
    method: 'POST',
    body: form,
  })
}

export function deleteCastingAudition(auditionId: number): Promise<void> {
  return apiFetch<void>(`/admin/casting/auditions/${auditionId}`, { method: 'DELETE' })
}
