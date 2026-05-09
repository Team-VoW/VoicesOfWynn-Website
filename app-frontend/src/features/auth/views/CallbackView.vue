<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { exchangeHandoffCode } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const error = ref<string>('')

const errorMessages: Record<string, string> = {
  access_denied: 'Sign-in was cancelled.',
  admin_required: 'Your account is not allowed to access this admin area.',
  external_oauth_failed: 'The sign-in provider did not respond successfully.',
  invalid_oauth_state: 'Sign-in state expired or was rejected.',
  invalid_provider: 'Unknown sign-in provider.',
  missing_authorization_code: 'Missing sign-in authorization code.',
}

onMounted(async () => {
  const code = new URLSearchParams(window.location.search).get('code')
  const providerError = typeof route.query.error === 'string' ? route.query.error : ''

  if (providerError) {
    error.value = errorMessages[providerError] ?? 'Sign-in failed.'
    return
  }

  if (!code) {
    error.value = 'Missing sign-in handoff code.'
    return
  }

  try {
    const tokens = await exchangeHandoffCode(code)
    auth.setTokens(tokens.accessToken, tokens.refreshToken, tokens.expiresAt)
    history.replaceState(null, '', window.location.pathname)
    void router.replace({ name: 'reports' })
    return
  } catch {
    error.value = 'Sign-in handoff expired or was rejected.'
  }
})
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background p-6">
    <div class="space-y-3 text-center">
      <p v-if="!error" class="text-sm text-muted-foreground">Signing you in…</p>
      <div v-else class="space-y-3">
        <p class="text-sm text-destructive">{{ error }}</p>
        <RouterLink to="/login" class="text-sm underline underline-offset-4">
          Back to login
        </RouterLink>
      </div>
    </div>
  </div>
</template>
