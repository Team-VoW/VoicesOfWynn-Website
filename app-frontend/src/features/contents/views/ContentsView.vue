<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { Search } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import { useQuests } from '../queries'

const { data, error, isPending, isError, refetch } = useQuests()

const search = ref('')

const quests = computed(() => data.value?.quests ?? [])

// The whole index is one small response, so filtering happens here rather than round-tripping
// a query for every keystroke.
const matches = computed(() => {
  const needle = search.value.trim().toLowerCase()
  if (!needle) return quests.value
  return quests.value.filter(
    (quest) =>
      quest.questName.toLowerCase().includes(needle) ||
      quest.questDegeneratedName.includes(needle.replace(/\s+/g, '')),
  )
})

const totalRecordings = computed(() =>
  quests.value.reduce((count, quest) => count + quest.recordingCount, 0),
)
</script>

<template>
  <section class="border-b border-[#a340c4]/15 bg-[#faf6fd]">
    <div class="mx-auto max-w-6xl px-4 py-14 text-center sm:px-6 md:py-16">
      <p
        class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]"
      >
        <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
        Every voiced line in the mod
      </p>
      <h1 class="mt-4 font-display text-2xl text-[#2a1438] sm:text-3xl">Mod Contents</h1>
      <p class="mx-auto mt-4 max-w-2xl text-lg leading-relaxed text-[#2a1438]/75">
        Curious what an NPC sounds like without doing the quest first? Pick a quest and listen to
        every recording in it.
      </p>

      <div class="relative mx-auto mt-8 max-w-md">
        <Search
          class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-[#2a1438]/40"
          aria-hidden="true"
        />
        <Input
          v-model="search"
          type="search"
          class="bg-white pl-9"
          placeholder="Search for a quest…"
          aria-label="Search for a quest"
        />
      </div>
    </div>
  </section>

  <section class="bg-white">
    <div class="mx-auto max-w-6xl px-4 py-14 sm:px-6 md:py-16">
      <div v-if="isPending" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton v-for="index in 12" :key="index" class="h-24 w-full rounded-2xl" />
      </div>

      <div
        v-else-if="isError"
        class="rounded-2xl border border-destructive/30 bg-destructive/5 p-8 text-center"
      >
        <p class="text-[#2a1438]">The quest list could not be loaded.</p>
        <p class="mt-2 text-sm text-[#2a1438]/65">{{ error?.message }}</p>
        <Button variant="outline" class="mt-6" @click="refetch()">Try again</Button>
      </div>

      <template v-else>
        <p class="text-sm text-[#2a1438]/60" aria-live="polite">
          <template v-if="search.trim()">
            {{ matches.length }} of {{ quests.length }} quests match “{{ search.trim() }}”
          </template>
          <template v-else>
            {{ quests.length }} quests &bull; {{ totalRecordings }} recordings
          </template>
        </p>

        <p v-if="!matches.length" class="mt-10 text-center text-lg text-[#2a1438]/65">
          No quest goes by that name.
        </p>

        <ul v-else class="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <li v-for="quest in matches" :key="quest.questId">
            <RouterLink
              :to="`/contents/${quest.questDegeneratedName}`"
              class="quest-card flex h-full flex-col justify-between gap-3 rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm transition-shadow hover:shadow-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
            >
              <h2 class="font-display text-base text-[#2a1438]">{{ quest.questName }}</h2>
              <p class="text-sm text-[#2a1438]/60">
                {{ quest.npcCount }} {{ quest.npcCount === 1 ? 'character' : 'characters' }}
                &bull;
                {{ quest.recordingCount }}
                {{ quest.recordingCount === 1 ? 'recording' : 'recordings' }}
              </p>
            </RouterLink>
          </li>
        </ul>
      </template>
    </div>
  </section>
</template>

<style scoped>
/* The same sweeping gradient rule the credit cards use, so every card on the site behaves alike. */
.quest-card {
  position: relative;
  overflow: hidden;
}

.quest-card::before {
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

.quest-card:hover::before,
.quest-card:focus-visible::before {
  transform: scaleX(1);
}

@media (prefers-reduced-motion: reduce) {
  .quest-card::before {
    transition: none;
  }
}
</style>
