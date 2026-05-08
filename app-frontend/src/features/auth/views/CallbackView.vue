<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { exchangeHandoffCode } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()
const error = ref<string>('')

onMounted(async () => {
  const code = new URLSearchParams(window.location.search).get('code')

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
