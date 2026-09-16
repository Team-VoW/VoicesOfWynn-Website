<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { FileText, ScrollText } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import type { WrittenQuest } from '@/api/types'

defineProps<{ quests: WrittenQuest[] }>()
</script>

<template>
  <ul class="grid gap-4 md:grid-cols-2">
    <li
      v-for="quest in quests"
      :key="quest.questId"
      class="credit-card flex flex-wrap items-center gap-x-4 gap-y-3 rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm"
    >
      <span
        class="flex size-11 shrink-0 items-center justify-center rounded-lg bg-[#a340c4]/12 text-[#7b1a9b]"
      >
        <ScrollText class="size-5" aria-hidden="true" />
      </span>

      <div class="min-w-0 flex-1 basis-40">
        <p class="text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Script for</p>
        <h3 class="mt-1 font-display text-base text-[#2a1438]">
          <RouterLink
            :to="`/contents/${quest.questDegeneratedName}`"
            class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
          >
            {{ quest.questName }}
          </RouterLink>
        </h3>
      </div>

      <Button v-if="quest.scriptUrl" as-child variant="brand" size="sm" class="shrink-0">
        <a :href="quest.scriptUrl" target="_blank" rel="noopener noreferrer">
          <FileText class="size-4" aria-hidden="true" />
          View script
        </a>
      </Button>
    </li>
  </ul>
</template>

<style scoped>
/* Same sweeping gradient rule as the contributor cards, so every card on the site behaves alike. */
.credit-card {
  position: relative;
  overflow: hidden;
}

.credit-card::before {
  content: '';
  position: absolute;
  inset-inline: 0;
  top: 0;
  height: 3px;
  background: linear-gradient(90deg, #7b1a9b, #ff6b9d);
  transform: scaleX(0);
  transform-origin: left;
  transition: transform 0.3s ease;
}

.credit-card:hover::before,
.credit-card:focus-within::before {
  transform: scaleX(1);
}

@media (prefers-reduced-motion: reduce) {
  .credit-card::before {
    transition: none;
  }
}
</style>
