import { apiFetch } from './client'
import type { AuthTokenResponse, RefreshTokenResponse } from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export function discordLoginUrl(): string {
  return `${API_BASE_URL}/auth/login/discord`
}

export function exchangeHandoffCode(code: string): Promise<AuthTokenResponse> {
  return apiFetch<AuthTokenResponse>('/auth/handoff', {
    method: 'POST',
    auth: false,
    body: { code },
  })
}

export function refreshAccessToken(refreshToken: string): Promise<RefreshTokenResponse> {
  return apiFetch<RefreshTokenResponse>('/auth/refresh', {
    method: 'POST',
    auth: false,
    body: { refreshToken },
  })
}
