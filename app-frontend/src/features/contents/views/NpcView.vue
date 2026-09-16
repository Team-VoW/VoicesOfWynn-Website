<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ArrowLeft, Drama } from 'lucide-vue-next'
import { ApiError } from '@/api/client'
import AudioPlayer from '@/components/audio/AudioPlayer.vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import CommentsDialog from '@/features/npcs/components/CommentsDialog.vue'
import VoteButtons from '@/features/npcs/components/VoteButtons.vue'
import { useNpcRecordings } from '@/features/npcs/queries'
import type { ContentCredit, NpcQuestCredit, NpcRecordingLine } from '@/api/types'
import { useMyNpcVote, useNpc } from '../queries'

const route = useRoute()

const npcId = computed(() => Number(route.params.npcId))
const { data: npc, isPending, isError, error } = useNpc(npcId)
const { data: myVote } = useMyNpcVote(npcId)

// This page is entirely about one NPC's lines, so there is nothing to wait for a click on.
const {
  data: recordingData,
  isPending: recordingsPending,
  isError: recordingsFailed,
} = useNpcRecordings(
  npcId,
  computed(() => true),
)

const commentsOpen = ref(false)

const notFound = computed(
  () => isError.value && error.value instanceof ApiError && error.value.status === 404,
)

interface QuestSection {
  quest: NpcQuestCredit
  recordings: NpcRecordingLine[]
}

// Driven by the NPC's quests rather than by its recordings, so a quest it has been cast in but
// not yet recorded for still shows up - as it did on the legacy page.
const sections = computed<QuestSection[]>(() => {
  const byQuest = new Map((recordingData.value?.quests ?? []).map((q) => [q.questId, q.recordings]))
  return (npc.value?.quests ?? []).map((quest) => ({
    quest,
    recordings: byQuest.get(quest.questId) ?? [],
  }))
})

const imageFailed = ref(false)

function onImageError(event: Event) {
  const image = event.target as HTMLImageElement
  if (npc.value && image.src !== npc.value.defaultImageUrl) image.src = npc.value.defaultImageUrl
  else imageFailed.value = true
}

function onAvatarError(event: Event, credit: ContentCredit) {
  const image = event.target as HTMLImageElement
  if (image.src !== credit.defaultAvatarUrl) image.src = credit.defaultAvatarUrl
}
</script>

<template>
  <div v-if="isPending" class="mx-auto max-w-4xl px-4 py-14 sm:px-6">
    <div class="flex flex-col items-center gap-5">
      <Skeleton class="size-32 rounded-2xl" />
      <Skeleton class="h-8 w-56" />
    </div>
    <div class="mt-12 space-y-3">
      <Skeleton v-for="index in 6" :key="index" class="h-12 w-full rounded-xl" />
    </div>
  </div>

  <div v-else-if="notFound" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">No such character</h1>
    <p class="mt-4 text-lg leading-relaxed text-[#2a1438]/70">
      Nobody in the mod goes by that id. The link may be out of date.
    </p>
    <Button as-child variant="brand" class="mt-8">
      <RouterLink :to="{ name: 'contents' }">Back to the quest list</RouterLink>
    </Button>
  </div>

  <div v-else-if="isError" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">This page could not be loaded</h1>
    <p class="mt-4 text-[#2a1438]/70">{{ error?.message }}</p>
  </div>

  <template v-else-if="npc">
    <section class="npc-hero relative overflow-hidden">
      <div
        class="relative z-[1] mx-auto flex max-w-4xl flex-col items-center gap-6 px-4 py-14 text-center sm:px-6 md:py-16"
      >
        <span
          v-if="imageFailed"
          class="flex size-28 items-center justify-center rounded-2xl bg-white/10 text-[#fbd057]"
        >
          <Drama class="size-12" aria-hidden="true" />
        </span>
        <img
          v-else
          :src="npc.imageUrl"
          :alt="`Portrait of ${npc.npcName}`"
          class="size-28 rounded-2xl bg-white/10 object-cover"
          @error="onImageError"
        />

        <div>
          <p
            class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#fbd057]"
          >
            <VoiceMark class="h-3.5 w-auto text-[#fbd057]" />
            Character
          </p>
          <h1 class="mt-4 flex flex-wrap items-center justify-center gap-3">
            <span class="font-display text-2xl text-white sm:text-4xl">{{ npc.npcName }}</span>
            <span
              v-if="npc.archived"
              class="rounded-full border border-white/25 px-3 py-1 text-sm text-white/70"
            >
              Outdated
            </span>
          </h1>
          <p class="mt-4 text-white/70">
            {{ npc.recordingCount }}
            {{ npc.recordingCount === 1 ? 'recording' : 'recordings' }}
            &bull; {{ npc.quests.length }}
            {{ npc.quests.length === 1 ? 'quest' : 'quests' }}
          </p>
        </div>
      </div>
    </section>

    <section class="bg-white">
      <div class="mx-auto max-w-4xl space-y-12 px-4 py-14 sm:px-6 md:py-16">
        <div
          class="flex flex-wrap items-center justify-between gap-x-6 gap-y-4 rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm"
        >
          <div class="flex min-w-0 items-center gap-4">
            <RouterLink
              v-if="npc.voiceActor"
              :to="`/cast/${npc.voiceActor.userId}`"
              class="shrink-0"
            >
              <img
                :src="npc.voiceActor.avatarUrl"
                :alt="`${npc.voiceActor.displayName}'s profile picture`"
                loading="lazy"
                class="size-12 rounded-full bg-[#faf6fd] object-cover"
                @error="onAvatarError($event, npc.voiceActor)"
              />
            </RouterLink>
            <div class="min-w-0">
              <p class="text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Voiced by</p>
              <p class="mt-1 font-display text-base text-[#2a1438]">
                <RouterLink
                  v-if="npc.voiceActor"
                  :to="`/cast/${npc.voiceActor.userId}`"
                  class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
                >
                  {{ npc.voiceActor.displayName }}
                </RouterLink>
                <span v-else class="italic text-[#2a1438]/55">Nobody yet</span>
              </p>
            </div>
          </div>

          <VoteButtons
            :npc-id="npc.npcId"
            :npc-name="npc.npcName"
            :upvotes="npc.upvotes"
            :downvotes="npc.downvotes"
            :comment-count="npc.commentCount"
            :my-vote="myVote?.myVote ?? null"
            @open-comments="commentsOpen = true"
          />
        </div>

        <section>
          <h2 class="font-display text-xl text-[#2a1438] sm:text-2xl">Recordings</h2>

          <div v-if="recordingsPending" class="mt-6 grid gap-2 sm:grid-cols-2">
            <Skeleton v-for="index in 6" :key="index" class="h-12 w-full rounded-xl" />
          </div>

          <p v-else-if="recordingsFailed" class="mt-6 text-sm text-destructive">
            The recordings could not be loaded.
          </p>

          <p v-else-if="!sections.length" class="mt-6 text-lg text-[#2a1438]/65">
            {{ npc.npcName }} has not been assigned to a quest yet.
          </p>

          <div v-else class="mt-6 space-y-10">
            <section v-for="section in sections" :key="section.quest.questId">
              <div class="flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
                <h3 class="font-display text-base text-[#2a1438]">
                  <RouterLink
                    :to="`/contents/${section.quest.questDegeneratedName}`"
                    class="text-[#7b1a9b] underline-offset-4 hover:underline"
                  >
                    {{ section.quest.questName }}
                  </RouterLink>
                </h3>

                <p
                  v-if="section.quest.soundEditor"
                  class="flex items-center gap-2 text-sm text-[#2a1438]/65"
                >
                  <span>Edited by</span>
                  <RouterLink
                    :to="`/cast/${section.quest.soundEditor.userId}`"
                    class="flex items-center gap-2 text-[#7b1a9b] underline-offset-4 hover:underline"
                  >
                    <img
                      :src="section.quest.soundEditor.avatarUrl"
                      alt=""
                      loading="lazy"
                      class="size-6 rounded-full bg-[#faf6fd] object-cover"
                      @error="onAvatarError($event, section.quest.soundEditor)"
                    />
                    {{ section.quest.soundEditor.displayName }}
                  </RouterLink>
                </p>
              </div>

              <p v-if="!section.recordings.length" class="mt-3 text-sm text-[#2a1438]/60">
                Nothing has been recorded for this quest yet.
              </p>

              <ul v-else class="mt-4 grid gap-2 sm:grid-cols-2">
                <li
                  v-for="recording in section.recordings"
                  :key="recording.recordingId"
                  class="flex items-center gap-3 rounded-xl border border-[#2a1438]/8 bg-[#faf6fd] px-3 py-2"
                >
                  <span class="w-10 shrink-0 text-xs tabular-nums text-[#2a1438]/50">
                    #{{ recording.line }}
                  </span>
                  <AudioPlayer
                    class="min-w-0 flex-1"
                    :src="recording.url"
                    :label="`line ${recording.line} of ${npc.npcName} in ${section.quest.questName}`"
                  />
                </li>
              </ul>
            </section>
          </div>
        </section>

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

    <CommentsDialog
      :npc="commentsOpen ? { npcId: npc.npcId, npcName: npc.npcName } : null"
      @update:npc="commentsOpen = $event !== null"
    />
  </template>
</template>

<style scoped>
.npc-hero {
  background: linear-gradient(160deg, #3b2159 0%, #2e1a47 45%, #1b0f2d 100%);
}

.npc-hero::after {
  content: '';
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(251, 208, 87, 0.55), transparent);
}
</style>
