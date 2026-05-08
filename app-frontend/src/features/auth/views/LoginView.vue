<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { discordLoginUrl } from '@/api/auth'

function login() {
  window.location.href = discordLoginUrl()
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background p-6">
    <div class="w-full max-w-sm space-y-6 rounded-xl border bg-card p-8 shadow-sm">
      <div class="space-y-2 text-center">
        <h1 class="text-2xl font-semibold tracking-tight">Voices of Wynn</h1>
        <p class="text-sm text-muted-foreground">Admin sign-in via Discord</p>
      </div>
      <Button class="w-full" @click="login">Login with Discord</Button>
    </div>
  </div>
</template>
