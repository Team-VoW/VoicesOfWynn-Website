import { apiFetch } from './client'
import type {
  AggregateUsageResponse,
  BroadcastListResponse,
  FunFactListResponse,
  ModRelease,
  SaveBroadcastRequest,
  SaveFunFactRequest,
  UpdateModReleaseRequest,
} from './types'

export function aggregateUsage(): Promise<AggregateUsageResponse> {
  return apiFetch<AggregateUsageResponse>('/admin/analytics/aggregate', { method: 'POST' })
}

export function getModRelease(signal?: AbortSignal): Promise<ModRelease> {
  return apiFetch<ModRelease>('/admin/mod/release', { signal })
}

export function updateModRelease(body: UpdateModReleaseRequest): Promise<void> {
  return apiFetch<void>('/admin/mod/release', { method: 'PUT', body })
}

export function getBroadcasts(signal?: AbortSignal): Promise<BroadcastListResponse> {
  return apiFetch<BroadcastListResponse>('/admin/mod/broadcasts', { signal })
}

export function createBroadcast(body: SaveBroadcastRequest): Promise<{ id: number }> {
  return apiFetch<{ id: number }>('/admin/mod/broadcasts', { method: 'POST', body })
}

export function updateBroadcast(id: number, body: SaveBroadcastRequest): Promise<void> {
  return apiFetch<void>(`/admin/mod/broadcasts/${id}`, { method: 'PUT', body })
}

export function deleteBroadcast(id: number): Promise<void> {
  return apiFetch<void>(`/admin/mod/broadcasts/${id}`, { method: 'DELETE' })
}

export function getFunFacts(signal?: AbortSignal): Promise<FunFactListResponse> {
  return apiFetch<FunFactListResponse>('/admin/mod/fun-facts', { signal })
}

export function createFunFact(body: SaveFunFactRequest): Promise<{ id: number }> {
  return apiFetch<{ id: number }>('/admin/mod/fun-facts', { method: 'POST', body })
}

export function updateFunFact(id: number, body: SaveFunFactRequest): Promise<void> {
  return apiFetch<void>(`/admin/mod/fun-facts/${id}`, { method: 'PUT', body })
}

export function deleteFunFact(id: number): Promise<void> {
  return apiFetch<void>(`/admin/mod/fun-facts/${id}`, { method: 'DELETE' })
}
