<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useEventListener } from '@vueuse/core'
import { LogIn, Menu, User, X } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { useSilentRefresh } from '@/features/auth/useSilentRefresh'

const auth = useAuthStore()
const route = useRoute()

// Anonymous visitors have no tokens, so this schedules nothing and calls no API.
useSilentRefresh()

const menuOpen = ref(false)

const navLinks = [
  { label: 'Home', to: { name: 'home' } as const },
  { label: 'Contents', to: { name: 'contents' } as const },
  { label: 'Credits', to: { name: 'credits' } as const },
  { label: 'FAQ', to: { name: 'faq' } as const },
]

const externalLinks = [
  {
    label: 'Patreon',
    href: 'https://www.patreon.com/Voices_Of_Wynn',
    icon: '/images/patreon.png',
  },
  {
    label: 'Discord',
    href: 'https://discord.gg/kuEK3XH4Y5',
    icon: '/images/discord.png',
  },
]

useEventListener(window, 'keydown', (event: KeyboardEvent) => {
  if (event.key === 'Escape') menuOpen.value = false
})

watch(
  () => route.fullPath,
  () => (menuOpen.value = false),
)
</script>

<template>
  <div class="flex min-h-screen flex-col bg-white text-[#2a1438]">
    <header class="sticky top-0 z-50 border-b border-[#a340c4]/30 bg-[#2e1a47]/95 backdrop-blur">
      <div class="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <RouterLink
          :to="{ name: 'home' }"
          class="flex items-center gap-3 text-white outline-none focus-visible:ring-2 focus-visible:ring-[#fbd057] focus-visible:ring-offset-2 focus-visible:ring-offset-[#2e1a47]"
        >
          <img src="/wynnvplogo.svg" alt="" class="size-9 shrink-0" />
          <span class="font-display text-sm tracking-wide sm:text-base">Voices of Wynn</span>
        </RouterLink>

        <nav class="hidden items-center gap-1 lg:flex" aria-label="Main">
          <RouterLink
            v-for="link in navLinks"
            :key="link.label"
            :to="link.to"
            exact-active-class="is-current"
            class="nav-link relative rounded-md px-3 py-2 text-[0.95rem] text-white transition-colors hover:text-[#fbd057] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#fbd057]"
            >{{ link.label }}</RouterLink
          >
          <span class="mx-2 h-6 w-px bg-white/20" />

          <a
            v-for="link in externalLinks"
            :key="link.label"
            :href="link.href"
            target="_blank"
            rel="noopener"
            class="flex items-center gap-2 rounded-md px-3 py-2 text-[0.95rem] text-white/85 transition-colors hover:text-[#fbd057] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#fbd057]"
          >
            <img :src="link.icon" alt="" class="size-5 rounded-sm" />
            {{ link.label }}
          </a>

          <Button v-if="auth.isAuthenticated" as-child variant="brand" size="sm" class="ml-2">
            <RouterLink :to="{ name: 'profile' }">
              <User class="size-4" aria-hidden="true" />
              My account
            </RouterLink>
          </Button>
          <Button v-else as-child variant="brand" size="sm" class="ml-2">
            <RouterLink :to="{ name: 'login' }">
              <LogIn class="size-4" aria-hidden="true" />
              Login
            </RouterLink>
          </Button>
        </nav>

        <button
          type="button"
          class="flex size-10 cursor-pointer items-center justify-center rounded-md text-white transition-colors hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#fbd057] lg:hidden"
          :aria-expanded="menuOpen"
          aria-controls="site-menu"
          :aria-label="menuOpen ? 'Close navigation menu' : 'Open navigation menu'"
          @click="menuOpen = !menuOpen"
        >
          <X v-if="menuOpen" class="size-6" aria-hidden="true" />
          <Menu v-else class="size-6" aria-hidden="true" />
        </button>
      </div>

      <nav
        v-show="menuOpen"
        id="site-menu"
        aria-label="Main"
        class="border-t border-white/10 bg-[#2e1a47] px-4 pb-5 pt-3 sm:px-6 lg:hidden"
      >
        <RouterLink
          v-for="link in navLinks"
          :key="link.label"
          :to="link.to"
          class="block rounded-md px-3 py-2.5 text-white transition-colors hover:bg-white/10"
          >{{ link.label }}</RouterLink
        >
        <span class="my-3 block h-px bg-white/15" />

        <a
          v-for="link in externalLinks"
          :key="link.label"
          :href="link.href"
          target="_blank"
          rel="noopener"
          class="flex items-center gap-2 rounded-md px-3 py-2.5 text-white/85 transition-colors hover:bg-white/10"
        >
          <img :src="link.icon" alt="" class="size-5 rounded-sm" />
          {{ link.label }}
        </a>

        <Button as-child variant="brand" class="mt-4 w-full">
          <RouterLink :to="auth.isAuthenticated ? { name: 'profile' } : { name: 'login' }">
            <component
              :is="auth.isAuthenticated ? User : LogIn"
              class="size-4"
              aria-hidden="true"
            />
            {{ auth.isAuthenticated ? 'My account' : 'Login' }}
          </RouterLink>
        </Button>
      </nav>
    </header>

    <main class="flex-1">
      <RouterView />
    </main>

    <footer class="bg-[#2e1a47] text-white/75">
      <div
        class="mx-auto flex max-w-6xl flex-col gap-3 px-4 py-8 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6"
      >
        <span>
          Coded by
          <a
            href="https://github.com/ShadyMedic"
            target="_blank"
            rel="noopener"
            class="text-[#fbd057] underline-offset-4 hover:underline"
            >Shady</a
          >
        </span>
        <span>
          Designed with &hearts; by
          <a
            href="https://github.com/Just5MoreMinutes"
            target="_blank"
            rel="noopener"
            class="text-[#fbd057] underline-offset-4 hover:underline"
            >Just5MoreMinutes</a
          >
          and
          <a
            href="https://github.com/kmaxii"
            target="_blank"
            rel="noopener"
            class="text-[#fbd057] underline-offset-4 hover:underline"
            >kmaxi</a
          >
        </span>
        <span>
          DevOps by
          <a
            href="https://github.com/kmaxii"
            target="_blank"
            rel="noopener"
            class="text-[#fbd057] underline-offset-4 hover:underline"
            >kmaxi</a
          >
        </span>
      </div>
    </footer>
  </div>
</template>

<style scoped>
/* Marks the page you are on. RouterLink's exact-active class drives it, so it follows the route
   rather than being painted on whichever link happens to be first. */
.nav-link.is-current::after {
  content: '';
  position: absolute;
  inset-inline: 0.75rem;
  bottom: 0.25rem;
  height: 2px;
  border-radius: 9999px;
  background-color: #fbd057;
}
</style>
