<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { toast } from 'vue-sonner'
import { DialogClose, DialogContent, DialogOverlay, DialogPortal, DialogRoot, DialogTitle } from 'reka-ui'
import { Plus, X } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import type { SaveCastingRoundRequest } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import RoundDetailsForm from '../components/RoundDetailsForm.vue'
import { formatDate, sourceLabel, statusClass, statusLabel } from '../castingFormat'
import { useAdminCastingRounds, useSaveCastingRound } from '../queries'

const router = useRouter()
const showArchived = ref(false)
const { data, isPending } = useAdminCastingRounds(showArchived)
const save = useSaveCastingRound()

const createOpen = ref(false)
const createError = ref('')
const rounds = computed(() => data.value?.rounds ?? [])

async function create(body: SaveCastingRoundRequest) {
  createError.value = ''
  try {
    const id = await save.mutateAsync({ id: null, body })
    createOpen.value = false
    toast.success('Round created. Add characters and auditions, then open it for voting.')
    await router.push({ name: 'casting-round-edit', params: { roundId: id } })
  } catch (err) {
    createError.value = messageFromContentError(err)
  }
}
</script>

<template>
  <div class="space-y-6">
    <header class="flex flex-wrap items-start justify-between gap-4">
      <div class="max-w-2xl space-y-1">
        <h1 class="text-xl font-semibold">Manage castings</h1>
        <p class="text-sm text-muted-foreground">
          Each round is one casting: its characters, their auditions and the votes on them. Voters
          only see open rounds. Close a round when voting is over and archive it once it is cast.
          Discord castings appear here as drafts when VowBot's <code>/setuppoll</code> runs.
        </p>
      </div>
      <Button type="button" @click="((createError = ''), (createOpen = true))">
        <Plus class="size-4" />
        New round
      </Button>
    </header>

    <label class="flex w-fit cursor-pointer items-center gap-2 text-sm text-muted-foreground">
      <input v-model="showArchived" type="checkbox" class="size-4 accent-[--brand-violet]" />
      Show archived rounds
    </label>

    <div v-if="isPending" class="space-y-2">
      <Skeleton v-for="n in 3" :key="n" class="h-16 w-full" />
    </div>

    <p v-else-if="rounds.length === 0" class="rounded-lg border p-8 text-center text-sm text-muted-foreground">
      No casting rounds yet.
    </p>

    <div v-else class="overflow-x-auto rounded-lg border">
      <table class="w-full min-w-[40rem] text-sm">
        <thead class="border-b bg-muted/40 text-left text-xs text-muted-foreground">
          <tr>
            <th class="px-4 py-2.5 font-medium">Round</th>
            <th class="px-4 py-2.5 font-medium">Status</th>
            <th class="px-4 py-2.5 font-medium">Source</th>
            <th class="px-4 py-2.5 text-right font-medium">Characters</th>
            <th class="px-4 py-2.5 text-right font-medium">Auditions</th>
            <th class="px-4 py-2.5 font-medium">Voting closes</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="round in rounds"
            :key="round.id"
            class="cursor-pointer border-b last:border-b-0 hover:bg-muted/40"
            @click="router.push({ name: 'casting-round-edit', params: { roundId: round.id } })"
          >
            <td class="px-4 py-3">
              <RouterLink
                :to="{ name: 'casting-round-edit', params: { roundId: round.id } }"
                class="font-medium hover:underline"
                @click.stop
              >
                {{ round.name }}
              </RouterLink>
              <p v-if="round.importStatus === 'Running'" class="text-xs text-muted-foreground">
                Importing… {{ round.importMessage }}
              </p>
            </td>
            <td class="px-4 py-3">
              <span class="rounded-full px-2.5 py-0.5 text-xs" :class="statusClass(round.status)">
                {{ statusLabel(round.status) }}
              </span>
              <span
                v-if="round.status === 'Open' && !round.votingOpen"
                class="ml-1.5 text-xs text-muted-foreground"
              >
                past closing date
              </span>
            </td>
            <td class="px-4 py-3 text-muted-foreground">{{ sourceLabel(round.source) }}</td>
            <td class="px-4 py-3 text-right tabular-nums">{{ round.characterCount }}</td>
            <td class="px-4 py-3 text-right tabular-nums">{{ round.auditionCount }}</td>
            <td class="px-4 py-3 text-muted-foreground">{{ formatDate(round.votingClosesAt) ?? '—' }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <DialogRoot :open="createOpen" @update:open="createOpen = $event">
      <DialogPortal>
        <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
        <DialogContent
          class="fixed top-1/2 left-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
        >
          <div class="mb-4 flex items-start justify-between gap-4">
            <DialogTitle class="text-lg font-semibold">New casting round</DialogTitle>
            <DialogClose
              aria-label="Close"
              class="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
            >
              <X class="size-4" />
            </DialogClose>
          </div>
          <RoundDetailsForm
            :saving="save.isPending.value"
            :error="createError"
            submit-label="Create round"
            @submit="create"
            @cancel="createOpen = false"
          />
        </DialogContent>
      </DialogPortal>
    </DialogRoot>
  </div>
</template>
