const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL
const configuredWebsiteBaseUrl = import.meta.env.VITE_WEBSITE_BASE_URL

if (!configuredApiBaseUrl) {
  throw new Error('VITE_API_BASE_URL is required')
}

function defaultWebsiteBaseUrl() {
  if (
    typeof window !== 'undefined' &&
    ['localhost', '127.0.0.1'].includes(window.location.hostname)
  ) {
    return 'http://localhost:8000'
  }

  return 'https://voicesofwynn.com'
}

export const API_BASE_URL = configuredApiBaseUrl.replace(/\/+$/, '')
export const WEBSITE_BASE_URL = (configuredWebsiteBaseUrl || defaultWebsiteBaseUrl()).replace(
  /\/+$/,
  '',
)
