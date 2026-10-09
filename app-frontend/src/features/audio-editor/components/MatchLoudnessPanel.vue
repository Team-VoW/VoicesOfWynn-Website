<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { LOUDNESS_PRESETS } from '@/features/tools/lib/audioChecks'
import { formatDb } from '../lib/format'
import {
  HEAVY_LIMITING_DB,
  planMatch,
  type MatchOptions,
  type PeakControl,
} from '../lib/matchLoudness'
import { useAudioWorkspace } from '../stores/workspace'
import MatchLoudnessRow from './MatchLoudnessRow.vue'

const workspace = useAudioWorkspace()
const progress = ref<{ label: string; done: number; total: number } | null>(null)

const scopeLabel = computed(() =>
  workspace.checkedDocuments.length > 0
    ? `${workspace.checkedDocuments.length} ticked file(s)`
    : `all ${workspace.documents.length} open file(s)`,
)

async function withProgress(
  label: string,
  total: number,
  task: (tick: (done: number) => void) => Promise<void>,
) {
  progress.value = { label, done: 0, total }
  try {
    await task((done) => {
      if (progress.value) progress.value.done = done
    })
  } catch (error) {
    toast.error(error instanceof Error ? error.message : `${label} failed.`)
  } finally {
    progress.value = null
  }
}

function analyzeAll() {
  const docs = workspace.batchDocuments
  void withProgress('Analyzing', docs.length, (tick) => workspace.analyzeAll(docs, tick))
}

const options = computed<MatchOptions>(() => ({
  targetLufs: workspace.targetLufs,
  ceilingDbtp: workspace.ceilingDbtp,
  peakControl: workspace.peakControl,
}))

const PEAK_CONTROLS: { id: PeakControl; label: string; title: string }[] = [
  {
    id: 'auto',
    label: 'Auto',
    title:
      'When the peaks have to come down more than a little, a gentle 3:1 compressor eases the loud syllables down first and the limiter only trims what is left. Sounds natural even on hard pushes.',
  },
  {
    id: 'limit',
    label: 'Limiter only',
    title:
      'A true-peak limiter alone, like Audition. Clean on lines that only go a few dB over; on lines pushed hard it flattens the loud syllables.',
  },
]

async function matchAll(saveAfter: boolean) {
  const docs = workspace.batchDocuments
  await withProgress('Matching', docs.length, async (tick) => {
    const { limited, heavy, short } = await workspace.matchAll(docs, options.value, tick)
    const notes = [
      limited ? `${limited} needed their peaks held under ${workspace.ceilingDbtp} dBTP.` : '',
      short.length
        ? `${short.length} could not reach ${workspace.targetLufs} LUFS without crushing them: ${short
            .map((entry) => `${entry.name} (${formatDb(entry.lufs, 'LUFS')})`)
            .join(', ')}.`
        : '',
      heavy.length
        ? `${heavy.length} needed over ${HEAVY_LIMITING_DB} dB of limiting, so give them a listen: ${heavy.join(', ')}.`
        : '',
    ]
      .filter(Boolean)
      .join(' ')
    const warn = short.length > 0 || heavy.length > 0
    if (!saveAfter) {
      const message = `Matched ${docs.length} file(s) to ${workspace.targetLufs} LUFS. ${notes}`
      if (warn) toast.warning(message, { duration: 15000 })
      else toast.success(message)
      return
    }
    const { written, zipped } = await workspace.saveAll(docs)
    const message = `Matched ${docs.length} file(s). ${notes} Saved ${written} over the originals${zipped ? `, ${zipped} in a ZIP download` : ''}.`
    if (warn) toast.warning(message, { duration: 15000 })
    else toast.success(message)
  })
}

/** Files that need their peaks held down at the current target, and the most any needs. */
const peakSummary = computed(() => {
  let count = 0
  let most = 0
  for (const doc of workspace.batchDocuments) {
    const plan = doc.analysis && planMatch(doc.analysis, options.value)
    if (!plan?.needsLimiting) continue
    count++
    most = Math.max(most, plan.reductionDb)
  }
  return { count, most }
})

const field =
  'h-8 w-20 rounded-md border bg-background px-2 text-sm tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ring/50'
</script>

<template>
  <div class="flex min-h-0 flex-col gap-4 text-sm">
    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Match loudness
      </h3>
      <div
        class="grid grid-cols-3 gap-1 rounded-md border bg-background p-1"
        role="radiogroup"
        aria-label="Loudness preset"
      >
        <button
          v-for="preset in LOUDNESS_PRESETS"
          :key="preset.id"
          type="button"
          role="radio"
          :aria-checked="workspace.targetLufs === preset.lufs"
          class="rounded px-2 py-1 text-xs font-medium transition-colors"
          :class="
            workspace.targetLufs === preset.lufs
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:bg-accent hover:text-foreground'
          "
          @click="workspace.targetLufs = preset.lufs"
        >
          {{ preset.label }}<br /><span class="tabular-nums opacity-80">{{ preset.lufs }}</span>
        </button>
      </div>
      <div class="flex items-center gap-2">
        <label for="match-target" class="w-24 text-xs text-muted-foreground">Loudness</label>
        <input
          id="match-target"
          v-model.number="workspace.targetLufs"
          type="number"
          step="0.5"
          max="0"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">LUFS</span>
      </div>
      <div class="flex items-center gap-2">
        <label for="match-ceiling" class="w-24 text-xs text-muted-foreground">True peak max</label>
        <input
          id="match-ceiling"
          v-model.number="workspace.ceilingDbtp"
          type="number"
          step="0.1"
          max="0"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">dBTP</span>
      </div>
      <div class="flex items-center gap-2">
        <span id="match-peaks" class="w-24 text-xs text-muted-foreground">Peaks</span>
        <div
          class="grid flex-1 grid-cols-2 gap-1 rounded-md border bg-background p-1"
          role="radiogroup"
          aria-labelledby="match-peaks"
        >
          <button
            v-for="control in PEAK_CONTROLS"
            :key="control.id"
            type="button"
            role="radio"
            :aria-checked="workspace.peakControl === control.id"
            :title="control.title"
            class="rounded px-2 py-0.5 text-xs font-medium transition-colors"
            :class="
              workspace.peakControl === control.id
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-accent hover:text-foreground'
            "
            @click="workspace.peakControl = control.id"
          >
            {{ control.label }}
          </button>
        </div>
      </div>
    </section>

    <section class="space-y-2">
      <p class="text-xs text-muted-foreground">Runs on {{ scopeLabel }}.</p>
      <div class="grid grid-cols-2 gap-2">
        <Button
          size="sm"
          variant="outline"
          :disabled="!!progress || !workspace.documents.length"
          @click="analyzeAll"
        >
          Analyze
        </Button>
        <Button
          size="sm"
          :disabled="!!progress || !workspace.documents.length"
          @click="matchAll(false)"
        >
          Match loudness
        </Button>
      </div>
      <Button
        size="sm"
        variant="brand"
        class="w-full"
        :disabled="!!progress || !workspace.documents.length"
        @click="matchAll(true)"
      >
        Match &amp; save all
      </Button>
      <div v-if="progress" class="space-y-1" role="status">
        <div class="flex items-center gap-2 text-xs text-muted-foreground">
          <Loader2 class="size-3 animate-spin" />
          {{ progress.label }} {{ progress.done }} / {{ progress.total }}
        </div>
        <div class="h-1.5 overflow-hidden rounded-full bg-muted">
          <div
            class="h-full bg-primary transition-[width]"
            :style="{ width: `${(progress.done / Math.max(1, progress.total)) * 100}%` }"
          />
        </div>
      </div>
    </section>

    <p
      v-if="peakSummary.count"
      class="-mb-2 text-xs text-amber-700 dark:text-amber-400"
      :title="`Reaching ${workspace.targetLufs} LUFS pushes their peaks past ${workspace.ceilingDbtp} dBTP, by up to ${peakSummary.most.toFixed(1)} dB before the extra gain that holding them down costs.`"
    >
      ⚑ {{ peakSummary.count }} of {{ workspace.batchDocuments.length }} need their peaks held down
      <template v-if="workspace.peakControl === 'auto'">(Auto compresses the hard ones)</template>
    </p>
    <div class="relative min-h-0 flex-1 overflow-auto rounded-md border">
      <table class="w-full text-xs tabular-nums">
        <thead class="sticky top-0 bg-muted text-left text-muted-foreground">
          <tr>
            <th class="px-2 py-1.5 font-medium">File</th>
            <th class="px-2 py-1.5 text-right font-medium">LUFS</th>
            <th class="px-2 py-1.5 text-right font-medium">Peak</th>
            <th
              class="px-2 py-1.5 text-right font-medium"
              title="Gain Match loudness will apply to reach the target. ⚑ marks files whose peaks must come down."
            >
              Change
            </th>
          </tr>
        </thead>
        <tbody>
          <MatchLoudnessRow
            v-for="doc in workspace.batchDocuments"
            :key="doc.id"
            :doc="doc"
            :active="doc.id === workspace.activeId"
            @select="workspace.setActive(doc.id)"
          />
        </tbody>
      </table>
    </div>
  </div>
</template>
