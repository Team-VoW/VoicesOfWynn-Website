<script setup lang="ts">
import { computed } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import SeekableAudioPlayer from '@/components/audio/SeekableAudioPlayer.vue'
import type { CastingMyPick, CastingRoundDetail } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useRemoveVote } from '../queries'

const props = defineProps<{
  round: CastingRoundDetail
  picks: CastingMyPick[]
}>()

const emit = defineEmits<{ open: [characterId: number] }>()

const unvote = useRemoveVote()

const cards = computed(() =>
  props.round.characters
    .map((character) => ({
      character,
      picks: props.picks
        .filter((p) => p.characterId === character.id && p.picked)
        .sort((a, b) => a.number - b.number),
      commentsOnly: props.picks
        .filter((p) => p.characterId === character.id && !p.picked)
        .sort((a, b) => a.number - b.number),
    }))
    .sort((a, b) => b.picks.length - a.picks.length),
)

async function remove(pick: CastingMyPick) {
  try {
    await unvote.mutateAsync({ characterId: pick.characterId, auditionId: pick.auditionId })
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <div class="grid gap-4 [grid-template-columns:repeat(auto-fill,minmax(min(100%,20rem),1fr))]">
    <article
      v-for="{ character, picks: characterPicks, commentsOnly } in cards"
      :key="character.id"
      class="flex flex-col gap-3 rounded-lg border bg-background p-4"
    >
      <header class="flex items-center justify-between gap-2">
        <div class="min-w-0">
          <h3 class="truncate font-semibold">{{ character.name }}</h3>
          <p class="text-xs text-muted-foreground">
            {{ characterPicks.length }} of {{ character.auditionCount }} picked<template
              v-if="character.questName"
            >
              · {{ character.questName }}</template
            >
          </p>
        </div>
        <span
          class="shrink-0 rounded-full px-2.5 py-0.5 text-xs"
          :class="character.done ? 'bg-emerald-50 text-emerald-800' : 'bg-primary/10 text-primary'"
        >
          {{ character.done ? 'Done' : 'In progress' }}
        </span>
      </header>

      <ul v-if="characterPicks.length > 0" class="space-y-1.5">
        <li v-for="pick in characterPicks" :key="pick.auditionId" class="space-y-1 rounded-md bg-primary/[0.04] p-2">
          <div class="flex items-center gap-2.5">
            <SeekableAudioPlayer
              compact
              :src="pick.audioUrl"
              :label="`audition ${pick.number} by ${pick.auditioneeName}`"
            />
            <span class="min-w-0 flex-1 truncate text-sm">#{{ pick.number }} {{ pick.auditioneeName }}</span>
            <button
              v-if="round.votingOpen && !character.done"
              type="button"
              class="cursor-pointer text-xs text-muted-foreground hover:text-foreground"
              @click="remove(pick)"
            >
              Remove
            </button>
          </div>
          <p class="pl-[2.875rem] text-xs break-words text-muted-foreground">
            {{ pick.comment || 'No comment' }}
          </p>
        </li>
      </ul>
      <p v-else class="text-sm text-muted-foreground">
        {{ character.done ? 'Abstained. No picks.' : 'No picks yet.' }}
      </p>

      <div v-if="commentsOnly.length > 0" class="space-y-1.5">
        <h4 class="text-xs font-medium text-muted-foreground">Comments without a vote</h4>
        <ul class="space-y-1.5">
          <li v-for="note in commentsOnly" :key="note.auditionId" class="rounded-md border border-dashed px-2.5 py-2 text-sm">
            <span class="text-muted-foreground">#{{ note.number }} {{ note.auditioneeName }}</span>
            <p class="text-xs break-words">{{ note.comment }}</p>
          </li>
        </ul>
      </div>

      <Button type="button" variant="outline" size="sm" class="mt-auto self-start" @click="emit('open', character.id)">
        Open character
      </Button>
    </article>
  </div>
</template>
