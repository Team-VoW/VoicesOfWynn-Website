<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ArrowLeft } from 'lucide-vue-next'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import CommentsDialog from '@/features/npcs/components/CommentsDialog.vue'
import NpcCard from '@/features/npcs/components/NpcCard.vue'
import type { NpcIdentity } from '@/features/npcs/types'
import type { VoteType } from '@/api/types'
import QuestProcessing from '../components/QuestProcessing.vue'
import { useQuest, useQuestVotes } from '../queries'

const route = useRoute()

const questName = computed(() => String(route.params.questName))
const { data: quest, isPending, isError, error } = useQuest(questName)
const { data: votes } = useQuestVotes(questName)

const commentsFor = ref<NpcIdentity | null>(null)

const notFound = computed(
  () => isError.value && error.value instanceof ApiError && error.value.status === 404,
)

const myVotes = computed(() => {
  const map = new Map<number, VoteType>()
  for (const npcId of votes.value?.upvoted ?? []) map.set(npcId, 'Up')
  for (const npcId of votes.value?.downvoted ?? []) map.set(npcId, 'Down')
  return map
})

const recordingCount = computed(() =>
  (quest.value?.npcs ?? []).reduce((count, npc) => count + npc.recordingCount, 0),
)
</script>

<template>
  <div v-if="isPending" class="mx-auto max-w-6xl px-4 py-14 sm:px-6">
    <Skeleton class="h-9 w-72" />
    <div class="mt-10 space-y-4">
      <Skeleton v-for="index in 5" :key="index" class="h-24 w-full rounded-2xl" />
    </div>
  </div>

  <div v-else-if="notFound" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">No such quest</h1>
    <p class="mt-4 text-lg leading-relaxed text-[#2a1438]/70">
      Nothing in the mod goes by that name. The link may be out of date.
    </p>
    <Button as-child variant="brand" class="mt-8">
      <RouterLink :to="{ name: 'contents' }">Back to the quest list</RouterLink>
    </Button>
  </div>

  <div v-else-if="isError" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">This page could not be loaded</h1>
    <p class="mt-4 text-[#2a1438]/70">{{ error?.message }}</p>
  </div>

  <template v-else-if="quest">
    <section class="quest-hero relative overflow-hidden">
      <div class="relative z-[1] mx-auto max-w-6xl px-4 py-14 text-center sm:px-6 md:py-16">
        <p
          class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#fbd057]"
        >
          <VoiceMark class="h-3.5 w-auto text-[#fbd057]" />
          Quest
        </p>
        <h1 class="mt-4 font-display text-2xl text-white sm:text-4xl">{{ quest.questName }}</h1>
        <p class="mt-4 text-white/70">
          {{ quest.npcs.length }} {{ quest.npcs.length === 1 ? 'character' : 'characters' }} &bull;
          {{ recordingCount }} {{ recordingCount === 1 ? 'recording' : 'recordings' }}
        </p>
      </div>
    </section>

    <section class="bg-white">
      <div class="mx-auto max-w-6xl space-y-14 px-4 py-14 sm:px-6 md:py-16">
        <section>
          <h2 class="font-display text-xl text-[#2a1438] sm:text-2xl">Cast</h2>

          <p v-if="!quest.npcs.length" class="mt-6 text-lg text-[#2a1438]/65">
            No characters have been cast for this quest yet.
          </p>

          <div v-else class="mt-6 space-y-4">
            <!-- The quest list is hidden and the recordings narrowed to this quest: every card
                 here is already in it, and an NPC may speak in several. -->
            <NpcCard
              v-for="npc in quest.npcs"
              :key="npc.npcId"
              :npc="npc"
              :my-vote="myVotes.get(npc.npcId) ?? null"
              :voice-actor="npc.voiceActor"
              :show-quests="false"
              :quest-id="quest.questId"
              @open-comments="commentsFor = $event"
            />
          </div>
        </section>

        <QuestProcessing :quest="quest" />

        <div>
          <Button as-child variant="outline">
            <RouterLink :to="{ name: 'contents' }">
              <ArrowLeft class="size-4" aria-hidden="true" />
              All quests
            </RouterLink>
          </Button>
        </div>
      </div>
    </section>

    <CommentsDialog :npc="commentsFor" @update:npc="commentsFor = $event" />
  </template>
</template>

<style scoped>
.quest-hero {
  background: linear-gradient(160deg, #3b2159 0%, #2e1a47 45%, #1b0f2d 100%);
}

.quest-hero::after {
  content: '';
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(251, 208, 87, 0.55), transparent);
}
</style>
