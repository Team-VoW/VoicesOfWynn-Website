import { apiFetch } from './client'
import { API_BASE_URL } from './config'
import type {
  AuthTokenResponse,
  PasswordLoginRequest,
  PasswordLoginResponse,
} from './types'

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

export function refreshAccessToken(refreshToken: string): Promise<AuthTokenResponse> {
  return apiFetch<AuthTokenResponse>('/auth/refresh', {
    method: 'POST',
    auth: false,
    body: { refreshToken },
  })
}

export function loginWithPassword(request: PasswordLoginRequest): Promise<PasswordLoginResponse> {
  return apiFetch<PasswordLoginResponse>('/auth/login/password', {
    method: 'POST',
    auth: false,
    body: request,
  })
}
