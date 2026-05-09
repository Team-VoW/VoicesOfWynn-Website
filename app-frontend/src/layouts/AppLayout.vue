<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { RouterView, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { TooltipProvider } from '@/components/ui/tooltip'
import { useAuthStore } from '@/stores/auth'
import { useSilentRefresh } from '@/features/auth/useSilentRefresh'

const auth = useAuthStore()
const { displayName } = storeToRefs(auth)
const router = useRouter()

useSilentRefresh()

async function logout() {
  auth.clear()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <TooltipProvider>
    <div class="min-h-screen bg-background text-foreground">
      <header class="border-b">
        <div class="flex items-center justify-between px-6 py-3">
          <RouterLink to="/admin/reports" class="flex items-center gap-2 text-base font-semibold">
            <img src="/wynnvplogo.svg" alt="Voices of Wynn logo" class="size-8 shrink-0" />
            <span>VoW Admin</span>
          </RouterLink>
          <div class="flex items-center gap-3 text-sm">
            <span v-if="displayName" class="text-muted-foreground">{{ displayName }}</span>
            <Button variant="ghost" size="sm" @click="logout">Logout</Button>
          </div>
        </div>
      </header>
      <main class="px-6 py-6">
        <RouterView />
      </main>
    </div>
  </TooltipProvider>
</template>
