import { useAuthStore } from '@/stores/auth'
import type { AuthTokenResponse } from './types'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public body: unknown,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  query?: Record<string, string | number | boolean | undefined | null>
  body?: unknown
  auth?: boolean
  signal?: AbortSignal
}

let inFlightRefresh: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const auth = useAuthStore()
  if (!auth.refreshToken) return null

  inFlightRefresh ??= (async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: auth.refreshToken }),
      })
      if (!res.ok) {
        // Only an explicit auth rejection means the refresh token is no longer valid.
        // Other failures (5xx, network/CORS) are transient — keep the session and let the caller retry.
        if (res.status === 400 || res.status === 401) {
          auth.clear()
        }
        return null
      }
      const data = (await res.json()) as AuthTokenResponse
      auth.setTokens(data.accessToken, data.refreshToken, data.expiresAt)
      return data.accessToken
    } catch {
      return null
    } finally {
      inFlightRefresh = null
    }
  })()

  return inFlightRefresh
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const url = new URL(`${API_BASE_URL}${path}`, window.location.origin)
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue
      url.searchParams.set(key, String(value))
    }
  }
  return url.toString()
}

export async function apiFetch<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const auth = useAuthStore()
  const useAuth = opts.auth !== false

  const exec = async (token: string | null): Promise<Response> => {
    const headers: Record<string, string> = {}
    if (opts.body !== undefined) headers['Content-Type'] = 'application/json'
    if (useAuth && token) headers.Authorization = `Bearer ${token}`

    return fetch(buildUrl(path, opts.query), {
      method: opts.method ?? 'GET',
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      signal: opts.signal,
    })
  }

  let res = await exec(useAuth ? auth.accessToken : null)

  if (res.status === 401 && useAuth && auth.refreshToken) {
    const fresh = await refreshAccessToken()
    if (fresh) {
      res = await exec(fresh)
    }
  }

  if (!res.ok) {
    let body: unknown = null
    try {
      body = await res.json()
    } catch {
      // ignore
    }
    throw new ApiError(res.status, `Request failed: ${res.status}`, body)
  }

  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
