<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowLeft } from 'lucide-vue-next'
import { ApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import type { VoicedNpc, VoteType } from '@/api/types'
import CastBio from '../components/CastBio.vue'
import CastProfileHeader from '../components/CastProfileHeader.vue'
import CastSocials from '../components/CastSocials.vue'
import CommentsDialog from '../components/CommentsDialog.vue'
import ScriptwritingSection from '../components/ScriptwritingSection.vue'
import SoundEditingSection from '../components/SoundEditingSection.vue'
import VoicedNpcCard from '../components/VoicedNpcCard.vue'
import { useContributor, useContributorVotes } from '../queries'

const route = useRoute()
const router = useRouter()

const userId = computed(() => Number(route.params.userId))
const { data: contributor, isPending, isError, error } = useContributor(userId)
const { data: votes } = useContributorVotes(userId)

const commentsFor = ref<VoicedNpc | null>(null)

const notFound = computed(
  () => isError.value && error.value instanceof ApiError && error.value.status === 404,
)

const myVotes = computed(() => {
  const map = new Map<number, VoteType>()
  for (const npcId of votes.value?.upvoted ?? []) map.set(npcId, 'Up')
  for (const npcId of votes.value?.downvoted ?? []) map.set(npcId, 'Down')
  return map
})

function goBack() {
  // Only step back when there is somewhere to step back to; a direct link has no history.
  if (window.history.length > 1) router.back()
  else router.push({ name: 'credits' })
}
</script>

<template>
  <div v-if="isPending" class="mx-auto max-w-6xl px-4 py-14 sm:px-6">
    <div class="flex flex-col items-center gap-5">
      <Skeleton class="size-36 rounded-full" />
      <Skeleton class="h-8 w-56" />
      <Skeleton class="h-4 w-72" />
    </div>
  </div>

  <div v-else-if="notFound" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">No such contributor</h1>
    <p class="mt-4 text-lg leading-relaxed text-[#2a1438]/70">
      There is nobody here. The link may be out of date.
    </p>
    <Button as-child variant="brand" class="mt-8">
      <RouterLink :to="{ name: 'credits' }">Back to the credits</RouterLink>
    </Button>
  </div>

  <div v-else-if="isError" class="mx-auto max-w-2xl px-4 py-20 text-center sm:px-6">
    <h1 class="font-display text-2xl text-[#2a1438]">This page could not be loaded</h1>
    <p class="mt-4 text-[#2a1438]/70">{{ error?.message }}</p>
  </div>

  <template v-else-if="contributor">
    <CastProfileHeader :contributor="contributor" />

    <section class="bg-white">
      <div class="mx-auto max-w-6xl space-y-14 px-4 py-14 sm:px-6 md:py-16">
        <CastSocials :contributor="contributor" />

        <section v-if="contributor.bio">
          <p class="flex items-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]">
            <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
            About
          </p>
          <div
            class="mt-5 rounded-2xl border border-[#2a1438]/10 bg-white p-6 text-[#2a1438]/85 shadow-sm sm:p-8"
          >
            <CastBio :bio="contributor.bio" />
          </div>
        </section>

        <section v-if="contributor.voicing.length">
          <p class="flex items-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]">
            <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
            Voicing
          </p>
          <h2 class="mt-4 font-display text-xl text-[#2a1438] sm:text-2xl">
            {{ contributor.voicing.length }} character{{
              contributor.voicing.length === 1 ? '' : 's'
            }}
          </h2>

          <div class="mt-8 space-y-4">
            <VoicedNpcCard
              v-for="npc in contributor.voicing"
              :key="npc.npcId"
              :npc="npc"
              :my-vote="myVotes.get(npc.npcId) ?? null"
              @open-comments="commentsFor = $event"
            />
          </div>
        </section>

        <section v-if="contributor.scriptwriting.length">
          <p class="flex items-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]">
            <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
            Scriptwriting
          </p>
          <h2 class="mt-4 font-display text-xl text-[#2a1438] sm:text-2xl">
            {{ contributor.scriptwriting.length }} quest{{
              contributor.scriptwriting.length === 1 ? '' : 's'
            }}
          </h2>
          <div class="mt-8">
            <ScriptwritingSection :quests="contributor.scriptwriting" />
          </div>
        </section>

        <section v-if="contributor.soundEditing.length">
          <p class="flex items-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]">
            <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
            Sound editing
          </p>
          <h2 class="mt-4 font-display text-xl text-[#2a1438] sm:text-2xl">
            {{ contributor.soundEditing.length }} quest{{
              contributor.soundEditing.length === 1 ? '' : 's'
            }}
          </h2>
          <div class="mt-8">
            <SoundEditingSection :quests="contributor.soundEditing" />
          </div>
        </section>

        <p
          v-if="
            !contributor.voicing.length &&
            !contributor.scriptwriting.length &&
            !contributor.soundEditing.length
          "
          class="text-lg text-[#2a1438]/65"
        >
          {{ contributor.displayName }} has no published contributions yet.
        </p>

        <div>
          <Button variant="outline" @click="goBack">
            <ArrowLeft class="size-4" aria-hidden="true" />
            Go back
          </Button>
        </div>
      </div>
    </section>

    <CommentsDialog :npc="commentsFor" @update:npc="commentsFor = $event" />
  </template>
</template>
