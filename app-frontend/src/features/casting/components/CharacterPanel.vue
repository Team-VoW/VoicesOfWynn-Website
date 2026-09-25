<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { X } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { CastingCharacterSummary } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import {
  useCastingAuditions,
  useCastVote,
  useClearCharacterVotes,
  useDeleteComment,
  useRemoveVote,
  useSetCharacterDone,
  useSetComment,
} from '../queries'
import AuditionRow, { type AuditionEditMode } from './AuditionRow.vue'

const props = defineProps<{
  character: CastingCharacterSummary
  votingOpen: boolean
}>()

const emit = defineEmits<{ markedDone: [characterId: number] }>()

const characterId = computed(() => props.character.id)
const { data, isPending } = useCastingAuditions(characterId)
const vote = useCastVote()
const unvote = useRemoveVote()
const clear = useClearCharacterVotes()
const setDone = useSetCharacterDone()
const setComment = useSetComment()
const deleteComment = useDeleteComment()

const search = ref('')
const onlyMine = ref(false)
const sortByName = ref(false)
const editing = ref<{ id: number; mode: AuditionEditMode } | null>(null)

watch(characterId, () => {
  search.value = ''
  onlyMine.value = false
  editing.value = null
})

const canEdit = computed(() => props.votingOpen && !props.character.done)
const auditions = computed(() => data.value?.auditions ?? [])
const picks = computed(() => auditions.value.filter((a) => a.myVote))

const rows = computed(() => {
  const needle = search.value.trim().toLowerCase()
  let list = auditions.value.filter(
    (a) => !needle || a.auditioneeName.toLowerCase().includes(needle) || String(a.number) === needle,
  )
  if (onlyMine.value) list = list.filter((a) => a.myVote)
  if (sortByName.value) list = [...list].sort((a, b) => a.auditioneeName.localeCompare(b.auditioneeName))
  return list
})

const revealNote = computed(() =>
  data.value?.commentsRevealed
    ? "Other staff's anonymous comments are visible below. Vote totals stay hidden."
    : "Other staff's anonymous comments appear once you mark this character done, so your vote isn't influenced. Vote totals stay hidden.",
)

async function run(action: () => Promise<unknown>) {
  try {
    await action()
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}

function submit(auditionId: number, mode: AuditionEditMode, comment: string) {
  editing.value = null
  const trimmed = comment.trim()
  if (mode === 'vote') {
    void run(() => vote.mutateAsync({ characterId: characterId.value, auditionId, comment: trimmed || null }))
  } else if (trimmed) {
    void run(() => setComment.mutateAsync({ characterId: characterId.value, auditionId, comment: trimmed }))
  } else {
    removeComment(auditionId)
  }
}

function removeComment(auditionId: number) {
  void run(() => deleteComment.mutateAsync({ characterId: characterId.value, auditionId }))
}

function removeVote(auditionId: number) {
  void run(() => unvote.mutateAsync({ characterId: characterId.value, auditionId }))
}

function playPick(auditionId: number) {
  document
    .querySelector<HTMLButtonElement>(`[data-testid="audition-${auditionId}"] button[aria-pressed]`)
    ?.click()
}

async function toggleDone() {
  const done = !props.character.done
  editing.value = null
  try {
    await setDone.mutateAsync({ characterId: characterId.value, done })
    if (done) emit('markedDone', characterId.value)
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <section class="flex min-w-0 flex-col gap-4" :aria-label="`Auditions for ${character.name}`">
    <div class="flex flex-wrap justify-between gap-5 rounded-lg border bg-background p-5">
      <div class="min-w-0 flex-[1_1_20rem] space-y-2">
        <div class="flex flex-wrap items-center gap-2">
          <h2 class="text-xl font-semibold">{{ character.name }}</h2>
          <span
            v-if="character.questName"
            class="rounded-full border border-primary/25 px-2.5 py-0.5 text-xs text-primary"
          >
            {{ character.questName }}
          </span>
          <span
            v-if="character.done"
            class="rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs text-emerald-800"
          >
            Done
          </span>
        </div>
        <p v-if="character.direction" class="max-w-2xl text-sm text-muted-foreground">
          {{ character.direction }}
        </p>
      </div>
      <div v-if="votingOpen" class="flex flex-col items-end justify-center gap-1.5">
        <Button v-if="!character.done" type="button" :disabled="setDone.isPending.value" @click="toggleDone">
          Mark character as done
        </Button>
        <Button v-else type="button" variant="outline" :disabled="setDone.isPending.value" @click="toggleDone">
          Reopen voting
        </Button>
        <span class="text-xs text-muted-foreground">
          <template v-if="!character.done">{{ picks.length }} of {{ character.auditionCount }} picked</template>
          <template v-else-if="picks.length > 0">
            {{ picks.length }} pick{{ picks.length === 1 ? '' : 's' }} submitted
          </template>
          <template v-else>Submitted as abstain</template>
        </span>
      </div>
    </div>

    <div class="rounded-lg border bg-primary/[0.02] px-5 py-4">
      <div class="mb-2.5 flex items-baseline justify-between">
        <h3 class="text-sm font-semibold">Your picks for {{ character.name }}</h3>
        <button
          v-if="picks.length > 0 && canEdit"
          type="button"
          class="cursor-pointer text-xs text-muted-foreground hover:text-foreground"
          @click="run(() => clear.mutateAsync({ characterId }))"
        >
          Clear all
        </button>
      </div>
      <ul v-if="picks.length > 0" class="flex flex-wrap gap-2">
        <li
          v-for="pick in picks"
          :key="pick.id"
          class="flex items-center gap-1.5 rounded-full border border-primary/30 bg-background py-1 pr-1.5 pl-3 text-sm text-primary"
        >
          <button type="button" class="cursor-pointer" :title="`Play #${pick.number}`" @click="playPick(pick.id)">
            #{{ pick.number }} {{ pick.auditioneeName }}
          </button>
          <button
            v-if="canEdit"
            type="button"
            class="flex size-5 cursor-pointer items-center justify-center rounded-full bg-primary/10"
            :aria-label="`Remove vote for ${pick.auditioneeName}`"
            @click="removeVote(pick.id)"
          >
            <X class="size-3" />
          </button>
        </li>
      </ul>
      <p v-else class="text-sm text-muted-foreground">
        Nothing picked yet. Hit Vote on any audition below, or leave a comment without voting. If you
        mark the character done with no picks, it counts as an abstain.
      </p>
      <p class="mt-3 text-xs text-muted-foreground">{{ revealNote }}</p>
    </div>

    <div class="overflow-hidden rounded-lg border bg-background">
      <div class="flex flex-wrap items-center gap-2 border-b px-4 py-3">
        <Input
          v-model="search"
          placeholder="Search auditionee"
          aria-label="Search auditionee"
          class="min-w-44 flex-1"
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          :class="onlyMine ? 'border-primary/30 bg-primary/10 text-primary' : ''"
          :aria-pressed="onlyMine"
          @click="onlyMine = !onlyMine"
        >
          Only my picks
        </Button>
        <Button type="button" variant="outline" size="sm" @click="sortByName = !sortByName">
          Sort: {{ sortByName ? 'Name A–Z' : 'Submitted' }}
        </Button>
      </div>

      <!-- Below sm each row stacks into a card (see AuditionRow), so the column header is hidden. -->
      <div class="sm:overflow-x-auto">
        <div class="sm:min-w-[36rem]">
          <div
            class="hidden grid-cols-[2rem_minmax(7rem,1fr)_minmax(10rem,1.6fr)_6.5rem] gap-3 border-b px-4 py-2 text-xs text-muted-foreground sm:grid"
          >
            <span>#</span><span>Auditionee</span><span>Audio</span><span class="text-right">Vote</span>
          </div>
          <div v-if="isPending" class="space-y-2 p-4">
            <Skeleton v-for="n in 4" :key="n" class="h-10 w-full" />
          </div>
          <ul v-else>
            <AuditionRow
              v-for="audition in rows"
              :key="audition.id"
              :audition="audition"
              :can-edit="canEdit"
              :editing="editing?.id === audition.id ? editing.mode : null"
              :saving="vote.isPending.value || setComment.isPending.value"
              @edit="(mode) => (editing = { id: audition.id, mode })"
              @cancel="editing = null"
              @submit="(mode, comment) => submit(audition.id, mode, comment)"
              @unvote="removeVote(audition.id)"
              @delete-comment="removeComment(audition.id)"
            />
          </ul>
          <p v-if="!isPending && rows.length === 0" class="p-8 text-center text-sm text-muted-foreground">
            No auditions match.
          </p>
        </div>
      </div>
    </div>

    <!-- Repeated below the list so a voter who has listened to everything need not scroll back up. -->
    <div
      v-if="votingOpen && auditions.length > 0"
      class="flex flex-wrap items-center justify-end gap-3 rounded-lg border bg-primary/[0.02] px-5 py-4"
    >
      <span class="text-sm text-muted-foreground">
        <template v-if="!character.done">
          {{ picks.length }} of {{ character.auditionCount }} picked.
          {{ picks.length === 0 ? 'Marking done with no picks counts as an abstain.' : '' }}
        </template>
        <template v-else>You marked {{ character.name }} as done.</template>
      </span>
      <Button v-if="!character.done" type="button" :disabled="setDone.isPending.value" @click="toggleDone">
        Mark character as done
      </Button>
      <Button v-else type="button" variant="outline" :disabled="setDone.isPending.value" @click="toggleDone">
        Reopen voting
      </Button>
    </div>
  </section>
</template>
