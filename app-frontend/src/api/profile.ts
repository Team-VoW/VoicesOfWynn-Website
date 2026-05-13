import { apiFetch } from './client'
import type { SelfProfile, SetSelfPasswordRequest, UpdateSelfProfileRequest } from './types'

export function getSelfProfile(signal?: AbortSignal): Promise<SelfProfile> {
  return apiFetch<SelfProfile>('/me', { signal })
}

export function updateSelfProfile(request: UpdateSelfProfileRequest): Promise<void> {
  return apiFetch<void>('/me', {
    method: 'PUT',
    body: request,
  })
}

export function setSelfPassword(request: SetSelfPasswordRequest): Promise<void> {
  return apiFetch<void>('/me/password', {
    method: 'PUT',
    body: request,
  })
}

export function uploadSelfAvatar(file: Blob, fileName = 'avatar.webp'): Promise<void> {
  const form = new FormData()
  form.append('file', file, fileName)
  return apiFetch<void>('/me/avatar', {
    method: 'PUT',
    body: form,
  })
}

export function clearSelfAvatar(): Promise<void> {
  return apiFetch<void>('/me/avatar', {
    method: 'DELETE',
  })
}
