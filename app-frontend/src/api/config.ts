const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL

if (!configuredApiBaseUrl) {
  throw new Error('VITE_API_BASE_URL is required')
}

export const API_BASE_URL = configuredApiBaseUrl.replace(/\/+$/, '')
