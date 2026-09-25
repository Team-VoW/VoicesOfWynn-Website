<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { toast } from 'vue-sonner'
import { ArrowLeft, Download, Pencil, Plus, Trash2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import type { CastingRoundStatus, SaveCastingCharacterRequest, SaveCastingRoundRequest } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import CharacterEditor from '../components/CharacterEditor.vue'
import CharacterForm from '../components/CharacterForm.vue'
import RoundDetailsForm from '../components/RoundDetailsForm.vue'
import { formatDate, sourceLabel, statusClass, statusLabel } from '../castingFormat'
import {
  useAdminCastingRound,
  useDeleteCastingRound,
  useImportCcc,
  useSaveCastingCharacter,
  useSaveCastingRound,
  useSetCastingRoundStatus,
} from '../queries'

const route = useRoute()
const router = useRouter()
const roundId = computed(() => {
  const value = Number(route.params.roundId)
  return Number.isInteger(value) && value > 0 ? value : null
})

const { data, isPending, error: loadError } = useAdminCastingRound(roundId)
const saveRound = useSaveCastingRound()
const setStatus = useSetCastingRoundStatus()
const deleteRound = useDeleteCastingRound()
const importCcc = useImportCcc()
const saveCharacter = useSaveCastingCharacter()

const round = computed(() => data.value?.round ?? null)
const characters = computed(() => data.value?.characters ?? [])
const editable = computed(() => round.value?.status === 'Draft' || round.value?.status === 'Open')

const editingDetails = ref(false)
const detailsError = ref('')
const addingCharacter = ref(false)
const characterError = ref('')
const cccUrl = ref('')
const confirmDelete = ref(false)

watch(
  round,
  (value) => {
    if (value?.source === 'Ccc' && !cccUrl.value) cccUrl.value = value.sourceRef ?? ''
  },
  { immediate: true },
)

const detailsInitial = computed<SaveCastingRoundRequest | null>(() =>
  round.value
    ? {
        name: round.value.name,
        description: round.value.description,
        votingClosesAt: round.value.votingClosesAt,
      }
    : null,
)

/** What a manager can do next from each status, in the order the buttons are shown. */
const actions = computed<{ status: CastingRoundStatus; label: string; primary?: boolean }[]>(() => {
  switch (round.value?.status) {
    case 'Draft':
      return [{ status: 'Open', label: 'Open for voting', primary: true }]
    case 'Open':
      return [
        { status: 'Closed', label: 'Close voting', primary: true },
        { status: 'Draft', label: 'Back to draft' },
      ]
    case 'Closed':
      return [
        { status: 'Archived', label: 'Archive', primary: true },
        { status: 'Open', label: 'Reopen voting' },
      ]
    case 'Archived':
      return [{ status: 'Closed', label: 'Unarchive' }]
    default:
      return []
  }
})

async function run(action: () => Promise<unknown>, success?: string) {
  try {
    await action()
    if (success) toast.success(success)
    return true
  } catch (err) {
    toast.error(messageFromContentError(err))
    return false
  }
}

async function saveDetails(body: SaveCastingRoundRequest) {
  if (roundId.value === null) return
  detailsError.value = ''
  try {
    await saveRound.mutateAsync({ id: roundId.value, body })
    editingDetails.value = false
    toast.success('Round saved.')
  } catch (err) {
    detailsError.value = messageFromContentError(err)
  }
}

function changeStatus(status: CastingRoundStatus) {
  if (roundId.value === null) return
  void run(() => setStatus.mutateAsync({ roundId: roundId.value!, status }), `Round is now ${statusLabel(status).toLowerCase()}.`)
}

async function destroy() {
  if (roundId.value === null) return
  if (!confirmDelete.value) {
    confirmDelete.value = true
    setTimeout(() => (confirmDelete.value = false), 4000)
    return
  }
  if (await run(() => deleteRound.mutateAsync(roundId.value!), 'Round deleted.')) {
    await router.push({ name: 'casting-rounds' })
  }
}

function startImport() {
  if (roundId.value === null || !cccUrl.value.trim()) return
  void run(
    () => importCcc.mutateAsync({ roundId: roundId.value!, url: cccUrl.value.trim() }),
    'Import started. New auditions appear here as they are converted.',
  )
}

async function addCharacter(body: SaveCastingCharacterRequest) {
  if (roundId.value === null) return
  characterError.value = ''
  try {
    await saveCharacter.mutateAsync({ roundId: roundId.value, id: null, body })
    addingCharacter.value = false
  } catch (err) {
    characterError.value = messageFromContentError(err)
  }
}

async function updateCharacter(id: number, body: SaveCastingCharacterRequest, done: (error: string) => void) {
  if (roundId.value === null) return
  try {
    await saveCharacter.mutateAsync({ roundId: roundId.value, id, body })
    done('')
  } catch (err) {
    done(messageFromContentError(err))
  }
}
</script>

<template>
  <div class="space-y-6">
    <Button as-child variant="ghost" size="sm" class="-ml-2">
      <RouterLink :to="{ name: 'casting-rounds' }">
        <ArrowLeft class="size-4" />
        All castings
      </RouterLink>
    </Button>

    <div v-if="isPending" class="space-y-3">
      <Skeleton class="h-24 w-full" />
      <Skeleton class="h-64 w-full" />
    </div>

    <p v-else-if="loadError || !round" class="rounded-lg border p-8 text-center text-sm text-muted-foreground">
      This casting round could not be found.
    </p>

    <template v-else>
      <section class="space-y-4 rounded-lg border bg-background p-5">
        <RoundDetailsForm
          v-if="editingDetails"
          :initial="detailsInitial"
          :saving="saveRound.isPending.value"
          :error="detailsError"
          submit-label="Save round"
          @submit="saveDetails"
          @cancel="editingDetails = false"
        />
        <template v-else>
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div class="min-w-0 space-y-1.5">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="text-xl font-semibold">{{ round.name }}</h1>
                <span class="rounded-full px-2.5 py-0.5 text-xs" :class="statusClass(round.status)">
                  {{ statusLabel(round.status) }}
                </span>
                <Button type="button" variant="ghost" size="icon-sm" aria-label="Edit round details" @click="editingDetails = true">
                  <Pencil class="size-4" />
                </Button>
              </div>
              <p v-if="round.description" class="max-w-2xl text-sm text-muted-foreground">{{ round.description }}</p>
              <p class="text-xs text-muted-foreground">
                {{ sourceLabel(round.source) }}<template v-if="round.sourceRef"> · {{ round.sourceRef }}</template>
                · {{ round.characterCount }} characters · {{ round.auditionCount }} auditions
                <template v-if="round.votingClosesAt"> · voting closes {{ formatDate(round.votingClosesAt) }}</template>
              </p>
              <p v-if="round.status === 'Open' && !round.votingOpen" class="text-sm text-amber-700">
                The closing date has passed, so voters can no longer change their votes. Close the round
                or move the date forward.
              </p>
            </div>
            <div class="flex flex-wrap gap-2">
              <Button
                v-for="action in actions"
                :key="action.status"
                type="button"
                :variant="action.primary ? 'default' : 'outline'"
                :disabled="setStatus.isPending.value"
                @click="changeStatus(action.status)"
              >
                {{ action.label }}
              </Button>
              <Button as-child variant="outline">
                <RouterLink :to="{ name: 'casting', query: { tab: 'review', round: round.id } }">Review votes</RouterLink>
              </Button>
              <Button
                v-if="round.status === 'Draft'"
                type="button"
                variant="ghost"
                :class="confirmDelete ? 'text-destructive' : ''"
                @click="destroy"
              >
                <Trash2 class="size-4" />
                {{ confirmDelete ? 'Click again to delete' : 'Delete' }}
              </Button>
            </div>
          </div>
        </template>
      </section>

      <section v-if="editable" class="space-y-3 rounded-lg border bg-background p-5">
        <div class="space-y-1">
          <h2 class="text-sm font-semibold">Import from Casting Call Club</h2>
          <p class="text-sm text-muted-foreground">
            Pulls every unsorted submission of the casting call into this round, one character per
            CCC role. Running it again only adds submissions that are new since last time.
          </p>
        </div>
        <form class="flex flex-wrap gap-2" @submit.prevent="startImport">
          <Input
            v-model="cccUrl"
            type="url"
            placeholder="https://www.castingcall.club/projects/…"
            aria-label="Casting Call Club link"
            class="min-w-64 flex-1"
          />
          <Button
            type="submit"
            variant="outline"
            :disabled="!cccUrl.trim() || importCcc.isPending.value || round.importStatus === 'Running'"
          >
            <Download class="size-4" />
            {{ round.importStatus === 'Running' ? 'Importing…' : 'Import' }}
          </Button>
        </form>
        <p
          v-if="round.importMessage && round.importStatus !== 'Idle'"
          class="text-sm"
          :class="round.importStatus === 'Failed' ? 'text-destructive' : 'text-muted-foreground'"
          role="status"
        >
          {{ round.importMessage }}
        </p>
      </section>

      <section class="space-y-3">
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-sm font-semibold">Characters</h2>
          <Button
            v-if="editable && !addingCharacter"
            type="button"
            variant="outline"
            size="sm"
            @click="((characterError = ''), (addingCharacter = true))"
          >
            <Plus class="size-4" />
            Add character
          </Button>
        </div>
        <p v-if="!editable" class="text-sm text-muted-foreground">
          The line-up of a {{ statusLabel(round.status).toLowerCase() }} round is frozen. Reopen voting to
          change characters or auditions; directions can still be edited.
        </p>

        <div v-if="addingCharacter" class="rounded-lg border bg-background p-4">
          <CharacterForm
            :saving="saveCharacter.isPending.value"
            :error="characterError"
            submit-label="Add character"
            @submit="addCharacter"
            @cancel="addingCharacter = false"
          />
        </div>

        <p
          v-if="characters.length === 0 && !addingCharacter"
          class="rounded-lg border p-6 text-center text-sm text-muted-foreground"
        >
          No characters yet. Add them by hand, import a CCC casting, or run <code>/setuppoll</code> in
          Discord.
        </p>

        <CharacterEditor
          v-for="character in characters"
          :key="character.id"
          :character="character"
          :editable="editable"
          :saving="saveCharacter.isPending.value"
          @save="(body, done) => updateCharacter(character.id, body, done)"
        />
      </section>
    </template>
  </div>
</template>
