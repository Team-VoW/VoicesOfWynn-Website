<script setup lang="ts">
import { computed, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Capabilities } from '@/lib/capabilities'
import { useAuthStore } from '@/stores/auth'
import AdminReviewPanel from '../components/AdminReviewPanel.vue'
import CharacterList from '../components/CharacterList.vue'
import CharacterPanel from '../components/CharacterPanel.vue'
import MyVotesGrid from '../components/MyVotesGrid.vue'
import { formatDate } from '../castingFormat'
import { useCastingRound, useMyCastingPicks, useOpenCastingRounds } from '../queries'

type Tab = 'characters' | 'mine' | 'review'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const canManage = computed(() => auth.hasCapability(Capabilities.CastingManage))

const { data: roundsData, isPending: roundsPending } = useOpenCastingRounds()
const openRounds = computed(() => roundsData.value?.rounds ?? [])

// Round, tab and character live in the query string so a link lands on the same place.
function queryNumber(name: string) {
  const value = Number(route.query[name])
  return Number.isInteger(value) && value > 0 ? value : null
}

const roundId = computed(() => {
  const requested = queryNumber('round')
  if (requested !== null && openRounds.value.some((r) => r.id === requested)) return requested
  return openRounds.value[0]?.id ?? null
})

const tab = computed<Tab>(() => {
  const requested = route.query.tab
  if (requested === 'mine') return 'mine'
  if (requested === 'review' && canManage.value) return 'review'
  return 'characters'
})

const { data: round, isPending: roundPending } = useCastingRound(roundId)
const { data: picksData } = useMyCastingPicks(roundId)

const characters = computed(() => round.value?.characters ?? [])
const selectedCharacter = computed(() => {
  const requested = queryNumber('character')
  return (
    characters.value.find((c) => c.id === requested) ??
    characters.value.find((c) => !c.done) ??
    characters.value[0] ??
    null
  )
})

const doneCount = computed(() => characters.value.filter((c) => c.done).length)
const donePercent = computed(() =>
  characters.value.length ? Math.round((doneCount.value / characters.value.length) * 100) : 0,
)
const totalPicks = computed(() => picksData.value?.picks.filter((pick) => pick.picked).length ?? 0)

const tabs = computed(() => {
  const list: { value: Tab; label: string }[] = [
    { value: 'characters', label: 'Characters' },
    { value: 'mine', label: `My votes · ${totalPicks.value}` },
  ]
  if (canManage.value) list.push({ value: 'review', label: 'Admin review' })
  return list
})

function navigate(query: Record<string, string | number | undefined>) {
  void router.replace({ query: { ...route.query, ...query } })
}

function selectCharacter(id: number) {
  navigate({ tab: undefined, character: id })
}

/** After marking done, move on to the next character the voter still has to do. */
function advanceFrom(characterId: number) {
  const list = characters.value
  const index = list.findIndex((c) => c.id === characterId)
  const next = [...list.slice(index + 1), ...list.slice(0, index)].find((c) => !c.done && c.id !== characterId)
  if (next) selectCharacter(next.id)
}

// Keep the URL honest if the requested round is no longer open. The review tab may point at a
// closed or archived round on purpose, so it is left alone.
watch(roundId, (id) => {
  if (tab.value !== 'review' && id !== null && queryNumber('round') !== null && queryNumber('round') !== id) {
    navigate({ round: id, character: undefined })
  }
})
</script>

<template>
  <div class="space-y-6">
    <header class="flex flex-wrap items-start justify-between gap-5">
      <div class="max-w-2xl space-y-1">
        <h1 class="font-display text-2xl">Casting</h1>
        <p class="text-sm text-muted-foreground">
          Listen to auditions and vote for as many as you like. You can add an optional comment to
          any vote. Comments are shown to other staff anonymously.
        </p>
      </div>
      <div class="flex flex-wrap gap-1 rounded-lg border p-1" role="tablist" aria-label="Casting views">
        <button
          v-for="item in tabs"
          :key="item.value"
          type="button"
          role="tab"
          class="cursor-pointer rounded-md px-4 py-2 text-sm transition-colors"
          :class="tab === item.value ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-muted'"
          :aria-selected="tab === item.value"
          @click="navigate({ tab: item.value === 'characters' ? undefined : item.value })"
        >
          {{ item.label }}
        </button>
      </div>
    </header>

    <AdminReviewPanel v-if="tab === 'review'" :initial-round-id="queryNumber('round') ?? roundId" />

    <template v-else>
      <div v-if="roundsPending" class="space-y-3">
        <Skeleton class="h-16 w-full" />
        <Skeleton class="h-96 w-full" />
      </div>

      <div
        v-else-if="openRounds.length === 0"
        class="space-y-3 rounded-lg border p-8 text-center text-sm text-muted-foreground"
      >
        <p>There is no casting open for voting right now.</p>
        <Button v-if="canManage" as-child variant="outline" size="sm">
          <RouterLink :to="{ name: 'casting-rounds' }">Manage castings</RouterLink>
        </Button>
      </div>

      <template v-else>
        <div class="flex flex-wrap items-center gap-x-6 gap-y-3 rounded-lg border px-5 py-4">
          <div class="flex min-w-0 items-center gap-3">
            <select
              v-if="openRounds.length > 1"
              :value="roundId ?? undefined"
              aria-label="Casting round"
              class="h-9 max-w-64 rounded-md border border-input bg-background px-3 text-sm font-medium shadow-xs"
              @change="navigate({ round: ($event.target as HTMLSelectElement).value, character: undefined })"
            >
              <option v-for="option in openRounds" :key="option.id" :value="option.id">{{ option.name }}</option>
            </select>
            <span v-else class="truncate font-medium">{{ round?.name }}</span>
          </div>
          <div class="text-sm whitespace-nowrap">
            <span class="text-xl font-semibold">{{ doneCount }}</span>
            <span class="text-muted-foreground"> / {{ characters.length }} characters done</span>
          </div>
          <div class="h-2 min-w-40 flex-1 overflow-hidden rounded-full bg-primary/10">
            <div
              class="h-full rounded-full transition-[width] duration-300"
              style="background: var(--brand-gradient)"
              :style="{ width: `${donePercent}%` }"
            />
          </div>
          <div v-if="round" class="text-sm text-muted-foreground">
            <template v-if="!round.votingOpen">Voting has closed</template>
            <template v-else-if="round.votingClosesAt">
              Voting closes <span class="text-foreground">{{ formatDate(round.votingClosesAt) }}</span>
            </template>
          </div>
        </div>
        <p v-if="round?.description" class="text-sm text-muted-foreground">{{ round.description }}</p>

        <div v-if="roundPending" class="grid gap-5 lg:grid-cols-[minmax(15rem,18rem)_minmax(0,1fr)]">
          <Skeleton class="h-96 w-full" />
          <Skeleton class="h-96 w-full" />
        </div>

        <MyVotesGrid
          v-else-if="tab === 'mine' && round"
          :round="round"
          :picks="picksData?.picks ?? []"
          @open="selectCharacter"
        />

        <div
          v-else-if="round"
          class="grid items-start gap-5 lg:grid-cols-[minmax(15rem,18rem)_minmax(0,1fr)]"
        >
          <CharacterList
            :characters="characters"
            :selected-id="selectedCharacter?.id ?? null"
            @select="selectCharacter"
          />
          <CharacterPanel
            v-if="selectedCharacter"
            :character="selectedCharacter"
            :voting-open="round.votingOpen"
            @marked-done="advanceFrom"
          />
          <p v-else class="rounded-lg border p-8 text-sm text-muted-foreground">
            This round has no characters yet.
          </p>
        </div>
      </template>
    </template>
  </div>
</template>
