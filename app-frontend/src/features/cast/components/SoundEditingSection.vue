<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { AudioLines } from 'lucide-vue-next'
import type { EditedQuest } from '@/api/types'

defineProps<{ quests: EditedQuest[] }>()
</script>

<template>
  <!--
    Grouped by quest with the NPCs as chips. The legacy page emitted one flat "NPC in Quest" row
    per pair, repeating the quest name once for every NPC edited in it.
  -->
  <ul class="grid gap-4 md:grid-cols-2">
    <li
      v-for="quest in quests"
      :key="quest.questId"
      class="credit-card rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm"
    >
      <div class="flex items-center gap-4">
        <span
          class="flex size-11 shrink-0 items-center justify-center rounded-lg bg-[#fbd057]/25 text-[#7b1a9b]"
        >
          <AudioLines class="size-5" aria-hidden="true" />
        </span>
        <div class="min-w-0">
          <p class="text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Edited for</p>
          <h3 class="mt-1 font-display text-base text-[#2a1438]">
            <RouterLink
              :to="`/contents/${quest.questDegeneratedName}`"
              class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
            >
              {{ quest.questName }}
            </RouterLink>
          </h3>
        </div>
      </div>

      <ul class="mt-4 flex flex-wrap gap-2">
        <li v-for="npc in quest.npcs" :key="npc.npcId">
          <RouterLink
            :to="`/contents/npc/${npc.npcId}`"
            class="inline-flex items-center rounded-full bg-[#a340c4]/10 px-3 py-1.5 text-sm text-[#7b1a9b] transition-colors hover:bg-[#a340c4]/20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
          >
            {{ npc.npcName }}
          </RouterLink>
        </li>
      </ul>
    </li>
  </ul>
</template>

<style scoped>
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
