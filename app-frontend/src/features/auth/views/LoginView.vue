<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LogIn } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { discordLoginUrl, loginWithPassword } from '@/api/auth'
import { WEBSITE_BASE_URL } from '@/api/config'
import { messageFromContentError } from '@/features/content/contentUtils'
import { firstAccessibleAdminRoute } from '@/lib/adminRoutes'
import { queryClient } from '@/lib/queryClient'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const credentials = reactive({
  username: '',
  password: '',
})

function login() {
  window.location.href = discordLoginUrl()
}

async function submitPasswordLogin() {
  error.value = ''
  loading.value = true
  try {
    const response = await loginWithPassword({
      username: credentials.username.trim(),
      password: credentials.password,
    })
    queryClient.clear()
    auth.setTokens(
      response.accessToken,
      response.refreshToken,
      response.expiresAt,
      response.forcePasswordChange,
    )
    await goAfterLogin()
  } catch (err) {
    error.value = messageFromContentError(err)
  } finally {
    loading.value = false
  }
}

async function goAfterLogin() {
  if (auth.forcePasswordChange) {
    await router.replace({ name: 'profile' })
    return
  }

  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : ''
  if (redirect) {
    await router.replace(redirect)
    return
  }

  await router.replace(firstAccessibleAdminRoute(auth.hasCapability) ?? { name: 'profile' })
}
</script>

<template>
  <div
    class="relative flex min-h-screen items-center justify-center overflow-hidden p-6"
    style="background: radial-gradient(ellipse at 50% 20%, #2e1a47 0%, #1a0f2e 55%, #0b0617 100%)"
  >
    <div class="relative z-10 w-full max-w-md space-y-6">
      <a
        :href="WEBSITE_BASE_URL"
        class="flex flex-col items-center gap-3 text-center text-white transition-transform hover:scale-[1.02]"
      >
        <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-16 drop-shadow-lg" />
        <h1 class="font-display text-3xl drop-shadow">Voices of Wynn</h1>
      </a>
      <p class="text-center">
        <a
          :href="WEBSITE_BASE_URL"
          class="text-sm text-white/85 underline-offset-4 hover:text-white hover:underline"
        >
          ← Back to main site
        </a>
      </p>

      <div class="space-y-6 rounded-xl border border-white/40 bg-card p-8 shadow-xl">
        <div class="space-y-1 text-center">
          <h2 class="font-display text-xl">Sign in</h2>
          <p class="text-sm text-muted-foreground">Use Discord or your account password</p>
        </div>
        <Button
          class="w-full gap-2 bg-[#5865F2] text-white hover:bg-[#4752c4]"
          @click="login"
        >
          <svg class="size-5" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M20.317 4.369a19.79 19.79 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.369a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.056 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994a.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128c.126-.094.252-.192.372-.291a.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.061 0a.074.074 0 0 1 .078.01c.12.099.246.197.373.291a.077.077 0 0 1-.006.128 12.298 12.298 0 0 1-1.873.891.077.077 0 0 0-.04.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.84 19.84 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.331c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z" />
          </svg>
          Login with Discord
        </Button>
        <div class="relative">
          <div class="absolute inset-0 flex items-center">
            <span class="w-full border-t" />
          </div>
          <div class="relative flex justify-center text-xs uppercase">
            <span class="bg-card px-2 text-muted-foreground">or</span>
          </div>
        </div>

        <div
          v-if="error"
          class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ error }}
        </div>

        <form class="space-y-4" @submit.prevent="submitPasswordLogin">
          <div class="space-y-2">
            <Label for="username">Email or display name</Label>
            <Input id="username" v-model="credentials.username" autocomplete="username" required />
          </div>
          <div class="space-y-2">
            <Label for="password">Password</Label>
            <Input id="password" v-model="credentials.password" type="password" autocomplete="current-password" required />
          </div>
          <Button
            type="submit"
            variant="brand"
            class="w-full gap-2"
            :disabled="loading || !credentials.username.trim() || !credentials.password"
          >
            <LogIn class="size-4" />
            Login
          </Button>
        </form>
      </div>
    </div>
  </div>
</template>
