<script setup lang="ts">
import { RouterLink, useRoute } from 'vue-router'
import { Home, Users } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import VoiceMark from '@/features/home/components/VoiceMark.vue'

const route = useRoute()
</script>

<template>
  <section class="bg-white">
    <div class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6 md:py-28">
      <p
        class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]"
      >
        <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
        Error 404
      </p>

      <h1 class="mt-5 font-display text-2xl text-[#2a1438] sm:text-3xl">Nothing to hear here</h1>

      <p class="mt-5 text-lg leading-relaxed text-[#2a1438]/75">
        This page has not been built yet, or it never existed. Some parts of the site are still
        being rebuilt and will open up as they are finished.
      </p>

      <p class="mt-4 break-all text-sm text-[#2a1438]/50">{{ route.fullPath }}</p>

      <div class="mt-9 flex flex-wrap justify-center gap-3">
        <Button as-child variant="brand">
          <RouterLink :to="{ name: 'home' }">
            <Home class="size-4" aria-hidden="true" />
            Back to the home page
          </RouterLink>
        </Button>
        <Button as-child variant="outline">
          <RouterLink :to="{ name: 'credits' }">
            <Users class="size-4" aria-hidden="true" />
            Browse the credits
          </RouterLink>
        </Button>
      </div>
    </div>
  </section>
</template>
