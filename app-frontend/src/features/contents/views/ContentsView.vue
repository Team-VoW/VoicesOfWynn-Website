<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { refDebounced } from '@vueuse/core'
import { Search } from 'lucide-vue-next'
import InfiniteScrollSentinel from '@/components/InfiniteScrollSentinel.vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import CommentsDialog from '@/features/npcs/components/CommentsDialog.vue'
import NpcCard from '@/features/npcs/components/NpcCard.vue'
import type { NpcIdentity } from '@/features/npcs/types'
import type { VoteType } from '@/api/types'
import { useNpcListVotes, useNpcs, useQuests } from '../queries'

const { data, error, isPending, isError, refetch } = useQuests()

const search = ref('')
const term = computed(() => search.value.trim())

const quests = computed(() => data.value?.quests ?? [])

// The whole quest index is one small response, so filtering happens here rather than round-tripping
// a query for every keystroke.
const matches = computed(() => {
  const needle = term.value.toLowerCase()
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

// The characters are paged by the server, so each keystroke would otherwise start a request.
const npcTerm = refDebounced(term, 300)

const {
  data: npcData,
  error: npcError,
  isPending: npcsPending,
  isError: npcsFailed,
  fetchNextPage,
  hasNextPage,
  isFetchingNextPage,
  refetch: refetchNpcs,
} = useNpcs(npcTerm)

const npcs = computed(() => npcData.value?.pages.flatMap((page) => page.results) ?? [])
const npcTotal = computed(() => npcData.value?.pages[0]?.total ?? 0)

const { data: votes } = useNpcListVotes(computed(() => npcs.value.map((npc) => npc.npcId)))

const myVotes = computed(() => {
  const map = new Map<number, VoteType>()
  for (const npcId of votes.value?.upvoted ?? []) map.set(npcId, 'Up')
  for (const npcId of votes.value?.downvoted ?? []) map.set(npcId, 'Down')
  return map
})

const commentsFor = ref<NpcIdentity | null>(null)
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
        Curious what an NPC sounds like without doing the quest first? Pick a quest, or scroll on to
        browse every character in the mod, and listen to their recordings.
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
          placeholder="Search for a quest or character…"
          aria-label="Search for a quest or character"
        />
      </div>
    </div>
  </section>

  <section class="bg-white">
    <div class="mx-auto max-w-6xl space-y-14 px-4 py-14 sm:px-6 md:py-16">
      <section>
        <h2 class="font-display text-xl text-[#2a1438] sm:text-2xl">Quests</h2>

        <div v-if="isPending" class="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Skeleton v-for="index in 12" :key="index" class="h-24 w-full rounded-2xl" />
        </div>

        <div
          v-else-if="isError"
          class="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-8 text-center"
        >
          <p class="text-[#2a1438]">The quest list could not be loaded.</p>
          <p class="mt-2 text-sm text-[#2a1438]/65">{{ error?.message }}</p>
          <Button variant="outline" class="mt-6" @click="refetch()">Try again</Button>
        </div>

        <template v-else>
          <p class="mt-2 text-sm text-[#2a1438]/60" aria-live="polite">
            <template v-if="term">
              {{ matches.length }} of {{ quests.length }} quests match “{{ term }}”
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
                <h3 class="font-display text-base text-[#2a1438]">{{ quest.questName }}</h3>
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
      </section>

      <!-- Past the quests, the same search keeps going over every character in the mod. -->
      <section>
        <h2 class="font-display text-xl text-[#2a1438] sm:text-2xl">Characters</h2>

        <div v-if="npcsPending" class="mt-6 space-y-4">
          <Skeleton v-for="index in 6" :key="index" class="h-24 w-full rounded-2xl" />
        </div>

        <div
          v-else-if="npcsFailed"
          class="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-8 text-center"
        >
          <p class="text-[#2a1438]">The characters could not be loaded.</p>
          <p class="mt-2 text-sm text-[#2a1438]/65">{{ npcError?.message }}</p>
          <Button variant="outline" class="mt-6" @click="refetchNpcs()">Try again</Button>
        </div>

        <template v-else>
          <!-- Infinite scroll is silent, so the count is announced as pages arrive. -->
          <p class="mt-2 text-sm text-[#2a1438]/60" aria-live="polite">
            <template v-if="npcTerm">
              Showing {{ npcs.length }} of {{ npcTotal }} characters matching “{{ npcTerm }}”
            </template>
            <template v-else> Showing {{ npcs.length }} of {{ npcTotal }} characters </template>
          </p>

          <p v-if="!npcs.length" class="mt-10 text-center text-lg text-[#2a1438]/65">
            No character goes by that name.
          </p>

          <div v-else class="mt-6 space-y-4">
            <NpcCard
              v-for="npc in npcs"
              :key="npc.npcId"
              :npc="npc"
              :my-vote="myVotes.get(npc.npcId) ?? null"
              :voice-actor="npc.voiceActor"
              @open-comments="commentsFor = $event"
            />
          </div>

          <div v-if="isFetchingNextPage" class="mt-10 flex justify-center">
            <p class="text-sm text-[#2a1438]/60">Loading more characters…</p>
          </div>

          <InfiniteScrollSentinel
            :disabled="!hasNextPage || isFetchingNextPage"
            @load="fetchNextPage()"
          />

          <!-- A manual control keeps the list reachable without a scroll gesture. -->
          <div v-if="hasNextPage && !isFetchingNextPage" class="mt-10 flex justify-center">
            <Button variant="outline" @click="fetchNextPage()">Show more characters</Button>
          </div>
        </template>
      </section>
    </div>
  </section>

  <CommentsDialog :npc="commentsFor" @update:npc="commentsFor = $event" />
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
