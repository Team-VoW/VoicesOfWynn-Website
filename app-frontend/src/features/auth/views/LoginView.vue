<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LogIn } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { discordLoginUrl, loginWithPassword } from '@/api/auth'
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
  <div class="flex min-h-screen items-center justify-center bg-background p-6">
    <div class="w-full max-w-md space-y-6 rounded-md border bg-card p-8 shadow-sm">
      <div class="space-y-2 text-center">
        <h1 class="text-2xl font-semibold tracking-tight">Voices of Wynn</h1>
        <p class="text-sm text-muted-foreground">Sign in with Discord or your account password</p>
      </div>
      <Button class="w-full" @click="login">Login with Discord</Button>
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
          class="w-full gap-2"
          :disabled="loading || !credentials.username.trim() || !credentials.password"
        >
          <LogIn class="size-4" />
          Login
        </Button>
      </form>
    </div>
  </div>
</template>
