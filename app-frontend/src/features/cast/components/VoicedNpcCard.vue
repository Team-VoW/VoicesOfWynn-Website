<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ChevronDown, Drama } from 'lucide-vue-next'
import AudioPlayer from '@/components/audio/AudioPlayer.vue'
import { Skeleton } from '@/components/ui/skeleton'
import type { VoicedNpc, VoteType } from '@/api/types'
import { useNpcRecordings } from '../queries'
import VoteButtons from './VoteButtons.vue'

const props = defineProps<{ npc: VoicedNpc; myVote: VoteType | null }>()
const emit = defineEmits<{ (e: 'open-comments', npc: VoicedNpc): void }>()

const open = ref(false)
const npcId = computed(() => props.npc.npcId)

// Recordings are fetched the first time the card opens, and then kept: a prolific actor's page
// would otherwise ship thousands of lines nobody has asked to hear.
const hasOpened = ref(false)
const { data, isPending, isError, error } = useNpcRecordings(
  npcId,
  computed(() => hasOpened.value),
)

const quests = computed(() => data.value?.quests ?? [])
const showQuestHeadings = computed(() => quests.value.length > 1)
const panelId = computed(() => `npc-recordings-${props.npc.npcId}`)

function toggle() {
  open.value = !open.value
  if (open.value) hasOpened.value = true
}

// Portraits fall back to the shared placeholder, and if that is missing too the icon below
// stands in - an NPC without art should still look deliberate rather than broken.
const imageFailed = ref(false)

function onImageError(event: Event) {
  const image = event.target as HTMLImageElement
  if (image.src !== props.npc.defaultImageUrl) image.src = props.npc.defaultImageUrl
  else imageFailed.value = true
}

const recordingLabel = computed(() =>
  props.npc.recordingCount === 1 ? '1 recording' : `${props.npc.recordingCount} recordings`,
)
</script>

<template>
  <article class="overflow-hidden rounded-2xl border border-[#2a1438]/10 bg-white shadow-sm">
    <div class="flex flex-wrap items-center gap-x-4 gap-y-3 p-4 sm:p-5">
      <!-- basis keeps the name and quest list readable: below it the actions wrap to their own
           row instead of squeezing the title into a two-word column. -->
      <div class="flex min-w-0 flex-1 basis-56 items-center gap-4">
        <span
          v-if="imageFailed"
          class="flex size-14 shrink-0 items-center justify-center rounded-lg bg-[#a340c4]/10 text-[#7b1a9b]"
        >
          <Drama class="size-6" aria-hidden="true" />
        </span>
        <img
          v-else
          :src="npc.imageUrl"
          alt=""
          loading="lazy"
          decoding="async"
          class="size-14 shrink-0 rounded-lg bg-[#faf6fd] object-cover"
          @error="onImageError"
        />

        <div class="min-w-0 flex-1">
          <h3 class="flex flex-wrap items-center gap-2 font-display text-base text-[#2a1438]">
            <RouterLink
              :to="`/contents/npc/${npc.npcId}`"
              class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
            >
              {{ npc.npcName }}
            </RouterLink>
            <span
              v-if="npc.archived"
              class="rounded-full bg-[#2a1438]/8 px-2 py-0.5 text-xs text-[#2a1438]/65"
            >
              Outdated
            </span>
          </h3>
          <p class="mt-1 text-sm text-[#2a1438]/65">
            <template v-if="npc.quests.length">
              in
              <template v-for="(quest, index) in npc.quests" :key="quest.questId">
                <RouterLink
                  :to="`/contents/${quest.questDegeneratedName}`"
                  class="text-[#7b1a9b] underline-offset-4 hover:underline"
                >
                  {{ quest.questName }}
                </RouterLink>
                <span v-if="index < npc.quests.length - 1">, </span>
              </template>
              &bull;
            </template>
            {{ recordingLabel }}
          </p>
        </div>
      </div>

      <div class="flex flex-1 items-center justify-end gap-1.5">
        <VoteButtons
          :npc-id="npc.npcId"
          :npc-name="npc.npcName"
          :upvotes="npc.upvotes"
          :downvotes="npc.downvotes"
          :comment-count="npc.commentCount"
          :my-vote="myVote"
          @open-comments="emit('open-comments', npc)"
        />

        <button
          type="button"
          class="flex size-9 cursor-pointer items-center justify-center rounded-lg text-[#2a1438]/60 transition-colors hover:bg-[#a340c4]/10 hover:text-[#7b1a9b] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
          :aria-expanded="open"
          :aria-controls="panelId"
          :aria-label="
            open ? `Hide recordings of ${npc.npcName}` : `Show recordings of ${npc.npcName}`
          "
          @click="toggle"
        >
          <ChevronDown
            class="size-5 transition-transform motion-reduce:transition-none"
            :class="open ? 'rotate-180' : ''"
            aria-hidden="true"
          />
        </button>
      </div>
    </div>

    <div v-show="open" :id="panelId" class="border-t border-[#2a1438]/10 bg-[#faf6fd] p-4 sm:p-5">
      <div v-if="isPending" class="space-y-3">
        <Skeleton v-for="index in 3" :key="index" class="h-9 w-full" />
      </div>

      <p v-else-if="isError" class="text-sm text-destructive">
        The recordings could not be loaded. {{ error?.message }}
      </p>

      <p v-else-if="!quests.length" class="text-sm text-[#2a1438]/65">
        There are no recordings to play for this NPC.
      </p>

      <div v-else class="space-y-6">
        <section v-for="quest in quests" :key="quest.questId">
          <h4 v-if="showQuestHeadings" class="mb-3 font-display text-sm text-[#2a1438]">
            <RouterLink
              :to="`/contents/${quest.questDegeneratedName}`"
              class="text-[#7b1a9b] underline-offset-4 hover:underline"
            >
              {{ quest.questName }}
            </RouterLink>
          </h4>

          <ul class="grid gap-2 sm:grid-cols-2">
            <li
              v-for="recording in quest.recordings"
              :key="recording.recordingId"
              class="flex items-center gap-3 rounded-xl border border-[#2a1438]/8 bg-white px-3 py-2"
            >
              <span class="w-10 shrink-0 text-xs tabular-nums text-[#2a1438]/50">
                #{{ recording.line }}
              </span>
              <AudioPlayer
                class="min-w-0 flex-1"
                :src="recording.url"
                :label="`line ${recording.line} of ${npc.npcName} in ${quest.questName}`"
              />
            </li>
          </ul>
        </section>
      </div>
    </div>
  </article>
</template>
