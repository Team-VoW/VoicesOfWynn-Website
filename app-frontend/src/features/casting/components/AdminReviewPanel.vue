<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { toast } from 'vue-sonner'
import { Crown, Settings } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import SeekableAudioPlayer from '@/components/audio/SeekableAudioPlayer.vue'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useAdminCastingRounds, useCastingReview, useSetCastingWinner } from '../queries'
import { statusLabel } from '../castingFormat'

/** Named review of one round. Any round can be reviewed, including closed and archived ones. */
const props = defineProps<{ initialRoundId: number | null }>()

const { data: roundsData } = useAdminCastingRounds(true)
const roundId = ref<number | null>(props.initialRoundId)
const selectedCharacterId = ref<number | null>(null)

watch(
  () => roundsData.value?.rounds,
  (rounds) => {
    if (roundId.value === null && rounds && rounds.length > 0) roundId.value = rounds[0]!.id
  },
  { immediate: true },
)

const { data: review, isPending } = useCastingReview(roundId)
const setWinner = useSetCastingWinner()

watch(roundId, () => (selectedCharacterId.value = null))

const characters = computed(() => review.value?.characters ?? [])
const selected = computed(
  () => characters.value.find((c) => c.id === selectedCharacterId.value) ?? characters.value[0] ?? null,
)
const eligible = computed(() => review.value?.eligibleVoterCount ?? 0)
const rankedWithVotes = computed(() => selected.value?.auditions.filter((a) => a.voteCount > 0) ?? [])
// Auditions nobody picked but someone commented on still belong in the review, after the ranked ones.
const commentedOnly = computed(
  () => selected.value?.auditions.filter((a) => a.voteCount === 0 && a.votes.length > 0) ?? [],
)
const listed = computed(() => [...rankedWithVotes.value, ...commentedOnly.value])
const zeroVotes = computed(() => (selected.value ? selected.value.auditionCount - listed.value.length : 0))
const topVotes = computed(() => rankedWithVotes.value[0]?.voteCount ?? 1)

async function toggleWinner(auditionId: number) {
  if (!selected.value) return
  const next = selected.value.winnerAuditionId === auditionId ? null : auditionId
  try {
    await setWinner.mutateAsync({ characterId: selected.value.id, auditionId: next })
    toast.success(next === null ? 'Winner cleared.' : 'Winner recorded.')
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center gap-3">
      <label for="review-round" class="text-sm text-muted-foreground">Round</label>
      <select
        id="review-round"
        v-model="roundId"
        class="h-9 min-w-56 rounded-md border border-input bg-background px-3 text-sm shadow-xs"
      >
        <option v-for="round in roundsData?.rounds ?? []" :key="round.id" :value="round.id">
          {{ round.name }} ({{ statusLabel(round.status) }})
        </option>
      </select>
      <Button v-if="roundId !== null" as-child variant="ghost" size="sm">
        <RouterLink :to="{ name: 'casting-round-edit', params: { roundId } }">
          <Settings class="size-4" />
          Manage round
        </RouterLink>
      </Button>
    </div>

    <div v-if="isPending && roundId !== null" class="space-y-2">
      <Skeleton v-for="n in 3" :key="n" class="h-16 w-full" />
    </div>

    <p v-else-if="characters.length === 0" class="rounded-lg border p-6 text-sm text-muted-foreground">
      This round has no characters yet.
    </p>

    <div v-else class="grid items-start gap-5 lg:grid-cols-[minmax(15rem,18rem)_minmax(0,1fr)]">
      <section
        class="flex max-h-80 flex-col overflow-hidden rounded-lg border bg-background lg:sticky lg:top-6 lg:max-h-[calc(100vh-3rem)]"
        aria-label="Characters"
      >
        <p class="border-b px-4 py-3 text-sm text-muted-foreground">
          Admin review. Voter names are visible only here.
        </p>
        <ul class="overflow-y-auto p-2">
          <li v-for="character in characters" :key="character.id">
            <button
              type="button"
              class="flex w-full cursor-pointer items-center gap-3 rounded-md px-3 py-2.5 text-left transition-colors"
              :class="
                character.id === selected?.id
                  ? 'bg-background shadow-sm ring-1 ring-primary/15 [box-shadow:inset_3px_0_0_var(--brand-pink)]'
                  : 'hover:bg-muted/70'
              "
              @click="selectedCharacterId = character.id"
            >
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-medium">{{ character.name }}</span>
                <span class="block text-xs text-muted-foreground">
                  {{ character.doneVoters.length }}/{{ eligible }} staff done · {{ character.totalVotes }} votes
                </span>
              </span>
              <span class="h-1.5 w-11 shrink-0 overflow-hidden rounded-full bg-primary/10" aria-hidden="true">
                <span
                  class="block h-full bg-emerald-600"
                  :style="{ width: `${eligible ? (character.doneVoters.length / eligible) * 100 : 0}%` }"
                />
              </span>
            </button>
          </li>
        </ul>
      </section>

      <section v-if="selected" class="flex min-w-0 flex-col gap-4">
        <div class="space-y-3 rounded-lg border bg-background p-5">
          <div class="flex flex-wrap items-center gap-2">
            <h2 class="text-xl font-semibold">{{ selected.name }}</h2>
            <span
              v-if="selected.questName"
              class="rounded-full border border-primary/25 px-2.5 py-0.5 text-xs text-primary"
            >
              {{ selected.questName }}
            </span>
          </div>
          <div class="flex flex-wrap gap-7 text-sm text-muted-foreground">
            <span><strong class="text-lg font-semibold text-foreground">{{ selected.totalVotes }}</strong> votes</span>
            <span>
              <strong class="text-lg font-semibold text-foreground">{{ selected.doneVoters.length }}/{{ eligible }}</strong>
              staff done
            </span>
            <span>
              <strong class="text-lg font-semibold text-foreground">{{ rankedWithVotes.length }}</strong>
              of {{ selected.auditionCount }} auditions with votes
            </span>
          </div>
          <p v-if="selected.pendingVoters.length > 0" class="text-sm text-muted-foreground">
            Still voting: <span class="text-foreground">{{ selected.pendingVoters.join(', ') }}</span>
          </p>
          <p v-if="selected.abstainedVoters.length > 0" class="text-sm text-muted-foreground">
            Abstained: <span class="text-foreground">{{ selected.abstainedVoters.join(', ') }}</span>
          </p>
        </div>

        <ol class="overflow-hidden rounded-lg border bg-background">
          <li
            v-for="(audition, index) in listed"
            :key="audition.id"
            class="space-y-2.5 border-b px-4 py-3.5 last:border-b-0"
            :class="selected.winnerAuditionId === audition.id ? 'bg-amber-50/60' : ''"
          >
            <div class="flex items-center gap-3">
              <span class="w-6 text-sm font-semibold" :class="index === 0 ? 'text-primary' : 'text-muted-foreground'">
                {{ audition.voteCount > 0 ? index + 1 : '–' }}
              </span>
              <SeekableAudioPlayer
                compact
                :src="audition.audioUrl"
                :label="`audition ${audition.number} by ${audition.auditioneeName}`"
              />
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm">#{{ audition.number }} {{ audition.auditioneeName }}</p>
                <div class="mt-1.5 h-1.5 max-w-90 overflow-hidden rounded-full bg-primary/10" aria-hidden="true">
                  <div
                    class="h-full rounded-full"
                    style="background: var(--brand-gradient)"
                    :style="{ width: `${(audition.voteCount / topVotes) * 100}%` }"
                  />
                </div>
              </div>
              <span class="shrink-0 text-sm">
                <strong class="font-semibold">{{ audition.voteCount }}</strong>
                <span class="text-muted-foreground"> vote{{ audition.voteCount === 1 ? '' : 's' }}</span>
              </span>
              <Button
                type="button"
                size="sm"
                :variant="selected.winnerAuditionId === audition.id ? 'default' : 'outline'"
                :disabled="setWinner.isPending.value"
                @click="toggleWinner(audition.id)"
              >
                <Crown class="size-4" />
                {{ selected.winnerAuditionId === audition.id ? 'Winner' : 'Set winner' }}
              </Button>
            </div>
            <ul class="space-y-1.5 sm:pl-[4.25rem]">
              <li
                v-for="(vote, voteIndex) in audition.votes"
                :key="voteIndex"
                class="flex gap-3 rounded-md bg-primary/[0.04] px-3 py-2 text-sm"
              >
                <span class="min-w-16 shrink-0 font-medium" :class="vote.picked ? 'text-primary' : 'text-muted-foreground'">
                  {{ vote.voterName }}
                </span>
                <span class="min-w-0 flex-1 break-words" :class="vote.comment ? '' : 'text-muted-foreground'">
                  {{ vote.comment || 'No comment' }}
                </span>
                <span v-if="!vote.picked" class="shrink-0 text-xs text-muted-foreground">comment only</span>
              </li>
            </ul>
          </li>
          <li v-if="listed.length === 0" class="px-4 py-6 text-sm text-muted-foreground">
            No votes yet.
          </li>
          <li v-if="zeroVotes > 0 && listed.length > 0" class="px-4 py-3.5 text-sm text-muted-foreground">
            {{ zeroVotes }} audition{{ zeroVotes === 1 ? '' : 's' }} received no votes or comments.
          </li>
        </ol>
      </section>
    </div>
  </div>
</template>
