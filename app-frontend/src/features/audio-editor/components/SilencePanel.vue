<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
import { computed, onMounted, ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import {
  LEADING_SILENCE_LIMIT,
  SILENCE_THRESHOLD_DB,
  TRAILING_SILENCE_LIMIT,
} from '@/features/tools/lib/audioChecks'
import { formatDb } from '../lib/format'
import {
  applyTrim,
  describeLoudEdges,
  edgeSoundAboveSilenceRule,
  explainLoudEdges,
  planTrim,
} from '../lib/silence'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'
import SilenceRow from './SilenceRow.vue'

const workspace = useAudioWorkspace()
const progress = ref<{ label: string; done: number; total: number } | null>(null)
const trimSettings = computed(() => workspace.trimSettings)

const doc = computed(() => {
  const active = workspace.activeDocument
  return active && active.status === 'ready' ? active : null
})

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

/**
 * Measures loaded files that have no analysis yet. Files still loading are measured as they load,
 * so starting them here would only read them twice.
 */
function analyzeMissing() {
  if (progress.value) return
  const docs = workspace.batchDocuments.filter(
    (entry) => entry.status === 'ready' && !entry.analysis && !entry.analyzing,
  )
  if (docs.length === 0) return
  void withProgress('Analyzing', docs.length, (tick) => workspace.analyzeAll(docs, tick))
}

// Opening the tab, ticking other files or opening more measures the new ones.
onMounted(analyzeMissing)
watch(() => workspace.batchDocuments.length, analyzeMissing)

function trimAll() {
  const docs = workspace.batchDocuments
  void withProgress('Trimming', docs.length, async (tick) => {
    const { trimmed } = await workspace.trimAll(docs, tick)
    toast.success(
      trimmed
        ? `Trimmed ${trimmed} of ${docs.length} file(s); the rest had nothing to cut.`
        : 'Nothing to cut in any of them.',
    )
  })
}

function overLimits(entry: EditorDocument) {
  const analysis = entry.analysis
  return (
    !!analysis &&
    (analysis.leadingSilenceSeconds > LEADING_SILENCE_LIMIT ||
      analysis.trailingSilenceSeconds > TRAILING_SILENCE_LIMIT)
  )
}

const summary = computed(() => {
  const docs = workspace.batchDocuments
  const measured = docs.filter((entry) => entry.analysis)
  return { measured: measured.length, over: measured.filter(overLimits).length }
})

const profile = computed(() => doc.value?.analysis?.silence ?? null)
const threshold = computed(() => (doc.value ? workspace.trimThresholdFor(doc.value) : 0))
const overridden = computed(() => doc.value?.trimThresholdDb != null)
// Recomputed as the threshold or padding moves; cheap (10 ms windows) and only for this file.
const plan = computed(() => {
  if (!doc.value) return null
  void doc.value.state
  return workspace.trimPlanFor(doc.value)
})
const cutsSomething = computed(
  () => !!plan.value && (plan.value.removedLeadSeconds > 0 || plan.value.removedTailSeconds > 0),
)

function onThresholdInput(event: Event) {
  if (doc.value)
    workspace.setTrimThreshold(doc.value, Number((event.target as HTMLInputElement).value))
}

async function trim() {
  const target = doc.value
  if (!target) return
  const thresholdDb = threshold.value
  const label = `Trim silence below ${Math.round(thresholdDb)} dB`
  try {
    await workspace.apply(target, label, (channels, current) => {
      const cut = planTrim(channels, current.sampleRate, {
        thresholdDb,
        keepLeadSeconds: trimSettings.value.keepLeadMs / 1000,
        keepTailSeconds: trimSettings.value.keepTailMs / 1000,
      })
      if (!cut || (cut.removedLeadSeconds === 0 && cut.removedTailSeconds === 0)) return null
      return { channels: applyTrim(channels, current.sampleRate, cut), selection: null, cursor: 0 }
    })
  } catch (error) {
    toast.error(error instanceof Error ? error.message : `${label} failed.`)
  }
}

const field =
  'h-7 w-14 rounded-md border bg-background px-1.5 text-xs tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ring/50'
</script>

<template>
  <div class="flex min-h-0 flex-col gap-3 text-sm">
    <section class="space-y-2">
      <div class="flex items-center gap-1.5 text-xs text-muted-foreground">
        <label for="silence-lead" title="Silence kept before and after the line">Keep</label>
        <input
          id="silence-lead"
          v-model.number="trimSettings.keepLeadMs"
          type="number"
          min="0"
          step="10"
          aria-label="Lead to keep in milliseconds"
          :class="field"
        />
        <span>/</span>
        <input
          id="silence-tail"
          v-model.number="trimSettings.keepTailMs"
          type="number"
          min="0"
          step="10"
          aria-label="Tail to keep in milliseconds"
          :class="field"
        />
        <span>ms</span>
        <label
          class="ml-auto flex items-center gap-1.5"
          title="Shade what gets cut on the waveform"
        >
          <input
            v-model="trimSettings.showPreview"
            type="checkbox"
            class="size-3.5 accent-primary"
          />
          Shade
        </label>
      </div>

      <template v-if="doc">
        <div class="flex items-center gap-2 text-xs">
          <label for="silence-threshold" class="shrink-0 text-muted-foreground">Cut below</label>
          <input
            id="silence-threshold"
            type="range"
            min="-80"
            max="-20"
            step="1"
            :value="Math.round(threshold)"
            class="min-w-0 flex-1 accent-primary"
            :title="
              profile
                ? `Suggested ${formatDb(profile.suggestedThresholdDb, 'dB', 0)}` +
                  (profile.noiseFloorDb !== null
                    ? `, background ${formatDb(profile.noiseFloorDb, 'dB', 0)}`
                    : '') +
                  `, speech ${formatDb(profile.speechDb, 'dB', 0)}`
                : undefined
            "
            @input="onThresholdInput"
          />
          <span class="w-12 shrink-0 text-right font-medium tabular-nums">{{
            formatDb(threshold, 'dB', 0)
          }}</span>
          <button
            v-if="overridden"
            type="button"
            class="shrink-0 text-primary underline-offset-2 hover:underline"
            :title="
              profile ? `Back to ${formatDb(profile.suggestedThresholdDb, 'dB', 0)}` : undefined
            "
            @click="workspace.setTrimThreshold(doc, null)"
          >
            Reset
          </button>
        </div>
        <p
          v-if="profile && edgeSoundAboveSilenceRule(profile)"
          class="rounded-md border border-amber-300 bg-amber-50 px-2 py-1 text-xs text-amber-900"
          :title="explainLoudEdges(profile)"
        >
          {{ describeLoudEdges(profile) }}, likely room noise or a breath.
        </p>
        <Button
          size="sm"
          variant="outline"
          class="w-full"
          :disabled="!cutsSomething"
          :title="!plan ? `Nothing rises above ${formatDb(threshold, 'dB', 0)}` : undefined"
          @click="trim"
        >
          <template v-if="cutsSomething && plan">
            Trim this file
            <span class="tabular-nums text-muted-foreground">
              −{{ plan.removedLeadSeconds.toFixed(2) }} s / −{{
                plan.removedTailSeconds.toFixed(2)
              }}
              s
            </span>
          </template>
          <template v-else>Nothing to trim in this file</template>
        </Button>
      </template>
    </section>

    <section class="space-y-2 border-t pt-3">
      <div class="flex items-center gap-2">
        <p
          class="min-w-0 flex-1 text-xs text-muted-foreground"
          :title="`Silence below ${SILENCE_THRESHOLD_DB} dB at each end, as the Audio check measures it. Limits: ${LEADING_SILENCE_LIMIT} s lead, ${TRAILING_SILENCE_LIMIT} s tail.`"
        >
          <span
            v-if="summary.measured"
            :class="summary.over ? 'font-medium text-amber-700 dark:text-amber-400' : ''"
          >
            {{ summary.over }} of {{ summary.measured }} over limits
          </span>
        </p>
        <Button
          size="sm"
          variant="outline"
          :disabled="!!progress || !workspace.documents.length"
          :title="`Trim ${scopeLabel}, each at its own threshold`"
          @click="trimAll"
        >
          {{
            workspace.checkedDocuments.length
              ? `Trim ${workspace.checkedDocuments.length} ticked`
              : `Trim all ${workspace.documents.length}`
          }}
        </Button>
      </div>
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

    <div class="relative min-h-0 flex-1 overflow-auto rounded-md border">
      <table class="w-full text-xs tabular-nums">
        <thead class="sticky top-0 bg-muted text-left text-muted-foreground">
          <tr>
            <th class="px-2 py-1.5 font-medium">File</th>
            <th class="px-2 py-1.5 text-right font-medium">Lead</th>
            <th class="px-2 py-1.5 text-right font-medium">Tail</th>
            <th class="px-2 py-1.5 text-right font-medium">Cut at</th>
          </tr>
        </thead>
        <tbody>
          <SilenceRow
            v-for="entry in workspace.batchDocuments"
            :key="entry.id"
            :doc="entry"
            :active="entry.id === workspace.activeId"
            @select="workspace.setActive(entry.id)"
          />
        </tbody>
      </table>
    </div>
  </div>
</template>
