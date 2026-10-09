<script setup lang="ts">
import { refDebounced, useLocalStorage } from '@vueuse/core'
import { Headphones, Loader2, Save, Trash2, X } from 'lucide-vue-next'
import { computed, markRaw, onBeforeUnmount, ref, watch, watchEffect, type ComputedRef } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { averageSpectrum } from '../lib/averageSpectrum'
import {
  CLEANUP_NAMES,
  CLEANUP_ORDER,
  describeStep,
  stepProblem,
  type CleanupKind,
  type CleanupStep,
} from '../lib/cleanupSteps'
import { runAnalysisJob } from '../lib/analysisClient'
import { staticReduction, type EqBand } from '../lib/filters'
import { formatDb } from '../lib/format'
import { captureNoisePrint } from '../lib/noiseReduction'
import { crop } from '../lib/operations'
import { peakInRange } from '../lib/peaks'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'
import CleanupSection from './CleanupSection.vue'
import EqCurve from './EqCurve.vue'
import ScrubNumber from './ScrubNumber.vue'

const props = defineProps<{ doc: EditorDocument }>()

const workspace = useAudioWorkspace()

const CLICK_THRESHOLDS = { low: 16, medium: 12, high: 8 } as const
const COMPRESSOR_KNEE_DB = 6
const EQ_NAMES = ['Low shelf', 'Bell 1', 'Bell 2', 'High shelf']

function defaultSettings() {
  return {
    highPassHz: 80,
    highPassSlope: 24 as 12 | 24 | 48,
    humBase: 50 as 50 | 60,
    humHarmonics: 4,
    humReductionDb: 30,
    noiseReductionDb: 12,
    noiseStrength: 1.5,
    clickSensitivity: 'medium' as keyof typeof CLICK_THRESHOLDS,
    deEssHz: 5000,
    deEssThresholdDb: -30,
    deEssMaxDb: 10,
    eq: [
      { type: 'lowshelf', frequency: 120, gainDb: 0, q: Math.SQRT1_2 },
      { type: 'peaking', frequency: 400, gainDb: 0, q: 1 },
      { type: 'peaking', frequency: 3000, gainDb: 0, q: 1 },
      { type: 'highshelf', frequency: 8000, gainDb: 0, q: Math.SQRT1_2 },
    ] as EqBand[],
    compThresholdDb: -20,
    compRatio: 3,
    compAttackMs: 5,
    compReleaseMs: 80,
    compMakeupDb: 0,
    /** Which steps Batch runs, always in panel order. */
    batchSteps: ['highPass', 'eq'] as CleanupKind[],
  }
}
type CleanupSettings = ReturnType<typeof defaultSettings>

const settings = useLocalStorage('vow.audioEditor.cleanup', defaultSettings(), {
  mergeDefaults: true,
})

const hasSelection = computed(() => !!props.doc.selection)
const scope = computed(() => (hasSelection.value ? 'selection' : 'whole file'))
const noisePrint = computed(() => workspace.noisePrint)
const printMatchesRate = computed(
  () => !noisePrint.value || noisePrint.value.sampleRate === props.doc.sampleRate,
)
const eqIsFlat = computed(() => settings.value.eq.every((band) => band.gainDb === 0))

// One computed per step, so changing the EQ does not re-render a preview of the de-esser.
const steps: Record<CleanupKind, ComputedRef<CleanupStep | null>> = {
  highPass: computed(() => ({
    kind: 'highPass',
    frequency: settings.value.highPassHz,
    slope: settings.value.highPassSlope,
  })),
  hum: computed(() => ({
    kind: 'hum',
    base: settings.value.humBase,
    harmonics: settings.value.humHarmonics,
    reductionDb: settings.value.humReductionDb,
  })),
  noise: computed(() =>
    noisePrint.value
      ? {
          kind: 'noise',
          print: noisePrint.value,
          options: {
            reductionDb: settings.value.noiseReductionDb,
            strength: settings.value.noiseStrength,
          },
        }
      : null,
  ),
  clicks: computed(() => ({
    kind: 'clicks',
    threshold: CLICK_THRESHOLDS[settings.value.clickSensitivity],
  })),
  deEss: computed(() => ({
    kind: 'deEss',
    options: {
      frequency: settings.value.deEssHz,
      thresholdDb: settings.value.deEssThresholdDb,
      maxReductionDb: settings.value.deEssMaxDb,
    },
  })),
  // Plain copies: steps go to the worker, which cannot take reactive proxies.
  eq: computed(() => ({ kind: 'eq', bands: settings.value.eq.map((band) => ({ ...band })) })),
  compress: computed(() => ({
    kind: 'compress',
    options: {
      thresholdDb: settings.value.compThresholdDb,
      ratio: Math.max(1, settings.value.compRatio),
      attackMs: settings.value.compAttackMs,
      releaseMs: settings.value.compReleaseMs,
      kneeDb: COMPRESSOR_KNEE_DB,
      makeupDb: settings.value.compMakeupDb,
    },
  })),
}

/** Why a step cannot run on this file right now, or null. */
function blockedReason(kind: CleanupKind) {
  const step = steps[kind].value
  if (!step) return 'Capture a noise print first.'
  return stepProblem(step, props.doc.sampleRate)
}

// Preview: the panel picks the step, the workspace renders it and playback plays the result.
const previewKind = ref<CleanupKind | null>(null)
watchEffect(() => {
  const step = previewKind.value ? steps[previewKind.value].value : null
  workspace.preview = step && !stepProblem(step, props.doc.sampleRate) ? markRaw(step) : null
})
watch(previewKind, () => (workspace.previewBypass = false))
// A spectral edit on the spectrogram started its own preview: this one is off.
watch(
  () => workspace.preview,
  (step) => {
    const kind = previewKind.value
    if (kind && step && step !== steps[kind].value) previewKind.value = null
  },
)
onBeforeUnmount(() => {
  if (!previewKind.value) return
  workspace.preview = null
  workspace.previewBypass = false
})

function togglePreview(kind: CleanupKind) {
  previewKind.value = previewKind.value === kind ? null : kind
}

function reportError(error: unknown, fallback: string) {
  toast.error(error instanceof Error ? error.message : fallback)
}

async function apply(kind: CleanupKind) {
  const step = steps[kind].value
  if (!step) return
  const doc = props.doc
  try {
    const problem = stepProblem(step, doc.sampleRate)
    if (problem) throw new Error(problem)
    const clicks = await workspace.applyStep(doc, step, (count) =>
      kind === 'clicks' ? `Remove ${count} click(s)` : describeStep(step),
    )
    if (kind === 'clicks' && clicks === 0) {
      toast.info(`No clicks found in the ${scope.value}.`)
      return
    }
    if (kind === 'clicks') toast.success(`Repaired ${clicks} click(s).`)
    // It is in the audio now; previewing it on top would play it twice.
    if (previewKind.value === kind) previewKind.value = null
  } catch (error) {
    reportError(error, `${CLEANUP_NAMES[kind]} failed.`)
  }
}

async function captureNoise() {
  const selection = props.doc.selection
  if (!selection) return
  try {
    const channels = await workspace.ensureChannels(props.doc)
    workspace.noisePrint = markRaw(
      captureNoisePrint(channels, props.doc.sampleRate, selection, props.doc.name),
    )
    toast.success('Noise print captured.')
  } catch (error) {
    reportError(error, 'The noise print could not be captured.')
  }
}

// Readouts that tell the person where to set things. They follow the audio (a new array on every
// edit), not `state`, which marker edits bump too. Cheap for a voice line (one pass each); on a
// long take the full-pass ones only measure a selection of reasonable length.
const READOUT_MAX_SECONDS = 120

/** The selection once it settles: dropping a marker or dragging a selection never waits on these. */
const settledSelection = refDebounced(
  computed(() => props.doc.selection),
  200,
)

/** The stretch the full-pass readouts measure, or null when too long to measure on the page. */
const readoutRange = computed(() => {
  const range = settledSelection.value ?? { start: 0, end: props.doc.frames }
  return range.end - range.start <= READOUT_MAX_SECONDS * props.doc.sampleRate ? range : null
})

// Measured in a worker: a full pass of filtering, about 100 ms per 30 s of audio.
const sibilancePeak = ref<number | null>(null)
watch(
  () => [props.doc.channels, readoutRange.value, settings.value.deEssHz] as const,
  async ([channels, range, frequency]) => {
    sibilancePeak.value = null
    if (!channels || !range) return
    const slice = crop(channels, range)
    const peak = await runAnalysisJob(
      { kind: 'sibilance', channels: slice, sampleRate: props.doc.sampleRate, frequency },
      slice.map((channel) => channel.buffer),
    )
    // Still the audio and settings it was measured for.
    if (
      props.doc.channels === channels &&
      readoutRange.value === range &&
      settings.value.deEssHz === frequency
    )
      sibilancePeak.value = peak
  },
  { immediate: true },
)

const spectrum = computed(() => {
  const channels = props.doc.channels
  return channels
    ? markRaw(averageSpectrum(channels, props.doc.sampleRate, settledSelection.value))
    : null
})

const compressorReadout = computed(() => {
  const peaks = props.doc.peaks
  const step = steps.compress.value
  if (!peaks || step?.kind !== 'compress') return null
  // From the waveform's peak summary rather than a pass over every sample.
  const selection = settledSelection.value
  const peak = peakInRange(peaks, selection?.start ?? 0, selection?.end ?? props.doc.frames)
  const peakDb = 20 * Math.log10(peak + 1e-12)
  return { peakDb, reductionDb: staticReduction(peakDb, step.options) }
})

// Presets: every setting above, saved under a name (one per voice actor or session, say).
const presets = useLocalStorage<{ name: string; settings: CleanupSettings }[]>(
  'vow.audioEditor.cleanupPresets',
  [],
)
const presetName = useLocalStorage('vow.audioEditor.cleanupPreset', '')
const naming = ref<string | null>(null)
const nameInput = ref<HTMLInputElement | null>(null)

function plain<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

const presetChanged = computed(() => {
  const preset = presets.value.find((entry) => entry.name === presetName.value)
  return !!preset && JSON.stringify(preset.settings) !== JSON.stringify(settings.value)
})

function loadPreset(name: string) {
  presetName.value = name
  const preset = presets.value.find((entry) => entry.name === name)
  if (preset) settings.value = { ...defaultSettings(), ...plain(preset.settings) }
}

function startSaving() {
  naming.value = presetName.value
  requestAnimationFrame(() => nameInput.value?.select())
}

function savePreset() {
  const name = naming.value?.trim()
  if (!name) return
  const entry = { name, settings: plain(settings.value) }
  const index = presets.value.findIndex((preset) => preset.name === name)
  if (index >= 0) presets.value.splice(index, 1, entry)
  else presets.value = [...presets.value, entry].sort((a, b) => a.name.localeCompare(b.name))
  presetName.value = name
  naming.value = null
  toast.success(`Saved preset “${name}”.`)
}

function deletePreset() {
  const index = presets.value.findIndex((preset) => preset.name === presetName.value)
  if (index < 0) return
  const [removed] = presets.value.splice(index, 1)
  presetName.value = ''
  toast(`Deleted preset “${removed!.name}”.`, {
    action: {
      label: 'Undo',
      onClick: () => {
        presets.value = [...presets.value, removed!].sort((a, b) => a.name.localeCompare(b.name))
        presetName.value = removed!.name
      },
    },
  })
}

// Batch: the ticked steps, in panel order, over whole files.
const batchProgress = ref<{ done: number; total: number } | null>(null)
const batchScopeLabel = computed(() =>
  workspace.checkedDocuments.length > 0
    ? `${workspace.checkedDocuments.length} ticked file(s)`
    : `all ${workspace.documents.length} open file(s)`,
)

function batchBlocked(kind: CleanupKind) {
  if (kind === 'noise' && !noisePrint.value) return 'Capture a noise print first.'
  if (kind === 'eq' && eqIsFlat.value) return 'Every EQ band is at 0 dB.'
  return null
}

function setBatchStep(kind: CleanupKind, on: boolean) {
  const current = settings.value.batchSteps.filter((entry) => entry !== kind)
  settings.value.batchSteps = on ? [...current, kind] : current
}

const batchChain = computed(() =>
  CLEANUP_ORDER.filter((kind) => settings.value.batchSteps.includes(kind) && !batchBlocked(kind))
    .map((kind) => steps[kind].value)
    .filter((step): step is CleanupStep => !!step),
)

async function runBatch() {
  const docs = workspace.batchDocuments.filter((doc) => doc.status !== 'error')
  const chain = batchChain.value
  if (chain.length === 0 || docs.length === 0) return
  batchProgress.value = { done: 0, total: docs.length }
  try {
    const { processed, skipped } = await workspace.cleanupAll(docs, chain, (done) => {
      if (batchProgress.value) batchProgress.value.done = done
    })
    const notes = [...skipped].map(
      ([kind, count]) =>
        ` ${CLEANUP_NAMES[kind]} skipped on ${count} file(s) at another sample rate.`,
    )
    toast.success(`Cleaned ${processed} file(s). Save all to write them.${notes.join('')}`)
  } catch (error) {
    reportError(error, 'Batch cleanup failed.')
  } finally {
    batchProgress.value = null
  }
}

const select = 'h-8 rounded-md border bg-background px-2 text-sm'
const hint = 'text-xs text-muted-foreground'
</script>

<template>
  <div class="space-y-5 text-sm">
    <div
      v-if="previewKind"
      class="sticky top-0 z-10 -mx-3 -mt-3 space-y-2 border-b bg-card px-3 py-2 shadow-sm"
      role="status"
    >
      <div class="flex items-center gap-2">
        <Headphones class="size-4 text-primary" />
        <p class="min-w-0 flex-1 truncate text-xs">
          Previewing <span class="font-medium">{{ CLEANUP_NAMES[previewKind] }}</span> on the
          {{ scope }}
        </p>
        <Button
          size="icon-sm"
          variant="ghost"
          aria-label="Stop previewing"
          title="Stop previewing"
          @click="previewKind = null"
          ><X
        /></Button>
      </div>
      <div class="flex items-center gap-2">
        <div
          class="inline-flex flex-1 rounded-md border bg-background p-0.5"
          role="group"
          aria-label="Hear (B)"
          title="Flip between the two with B while playing"
        >
          <button
            v-for="option in [
              { bypass: false, label: 'Processed' },
              { bypass: true, label: 'Original' },
            ]"
            :key="option.label"
            type="button"
            :aria-pressed="workspace.previewBypass === option.bypass"
            class="flex-1 rounded px-2 py-1 text-xs font-medium"
            :class="
              workspace.previewBypass === option.bypass
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:text-foreground'
            "
            @click="workspace.previewBypass = option.bypass"
          >
            {{ option.label }}
          </button>
        </div>
        <Button size="sm" @click="apply(previewKind)">Apply</Button>
      </div>
      <p :class="hint">Play with Space; B flips between processed and original.</p>
    </div>

    <p v-else :class="hint">
      Cleanup applies to the <span class="font-medium text-foreground">{{ scope }}</span> and can be
      undone with Ctrl+Z. Turn on <span class="font-medium text-foreground">Preview</span> on a step
      to hear it while you adjust it.
    </p>

    <section class="space-y-2">
      <div class="flex items-center gap-1">
        <select
          :value="presetName"
          aria-label="Preset"
          :class="[select, 'min-w-0 flex-1']"
          @change="loadPreset(($event.target as HTMLSelectElement).value)"
        >
          <option value="" disabled>
            {{ presets.length ? 'Load a preset…' : 'No presets yet' }}
          </option>
          <option v-for="preset in presets" :key="preset.name" :value="preset.name">
            {{ preset.name }}{{ preset.name === presetName && presetChanged ? ' (changed)' : '' }}
          </option>
        </select>
        <Button
          size="icon-sm"
          variant="outline"
          aria-label="Save as preset"
          title="Save these settings as a preset"
          @click="startSaving"
          ><Save
        /></Button>
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!presetName"
          aria-label="Delete preset"
          title="Delete this preset"
          @click="deletePreset"
          ><Trash2
        /></Button>
      </div>
      <form v-if="naming !== null" class="flex items-center gap-1" @submit.prevent="savePreset">
        <input
          ref="nameInput"
          v-model="naming"
          aria-label="Preset name"
          placeholder="Name, e.g. Azael"
          class="h-8 min-w-0 flex-1 rounded-md border bg-background px-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
          @keydown.esc.stop="naming = null"
        />
        <Button size="sm" type="submit" :disabled="!naming.trim()">Save</Button>
        <Button size="sm" variant="ghost" type="button" @click="naming = null">Cancel</Button>
      </form>
    </section>

    <CleanupSection
      title="Rumble"
      :previewing="previewKind === 'highPass'"
      @toggle-preview="togglePreview('highPass')"
    >
      <div class="flex items-center gap-2">
        <ScrubNumber
          id="cleanup-highpass"
          v-model="settings.highPassHz"
          label="High-pass frequency"
          :min="20"
          :max="300"
          log
          unit="Hz"
        />
        <select
          v-model.number="settings.highPassSlope"
          aria-label="High-pass slope"
          :class="select"
        >
          <option :value="12">12 dB/oct</option>
          <option :value="24">24 dB/oct</option>
          <option :value="48">48 dB/oct</option>
        </select>
      </div>
      <Button size="sm" variant="outline" class="w-full" @click="apply('highPass')"
        >High-pass</Button
      >
      <p :class="hint">
        Removes rumble, mic-stand bumps and plosive thumps below the frequency. 80 Hz is safe for
        any voice.
      </p>
    </CleanupSection>

    <CleanupSection
      title="Hum"
      :previewing="previewKind === 'hum'"
      @toggle-preview="togglePreview('hum')"
    >
      <div class="flex items-center gap-2">
        <select v-model.number="settings.humBase" aria-label="Mains frequency" :class="select">
          <option :value="50">50 Hz (EU)</option>
          <option :value="60">60 Hz (US)</option>
        </select>
        <ScrubNumber
          id="cleanup-harmonics"
          v-model="settings.humHarmonics"
          label="Harmonics"
          :min="1"
          :max="10"
          title="How many harmonics to remove"
        />
        <span :class="hint">harmonics</span>
      </div>
      <div class="flex items-center gap-2">
        <ScrubNumber
          id="cleanup-hum-reduction"
          v-model="settings.humReductionDb"
          label="Hum reduction"
          :min="1"
          :max="60"
          unit="dB"
        />
        <Button size="sm" variant="outline" class="ml-auto" @click="apply('hum')"
          >Remove hum</Button
        >
      </div>
    </CleanupSection>

    <CleanupSection
      title="Noise reduction"
      :previewing="previewKind === 'noise'"
      :preview-blocked="blockedReason('noise')"
      @toggle-preview="togglePreview('noise')"
    >
      <div class="flex items-center gap-2">
        <Button size="sm" variant="outline" :disabled="!hasSelection" @click="captureNoise">
          Capture noise print
        </Button>
      </div>
      <p :class="hint">
        <template v-if="noisePrint">
          Print: {{ noisePrint.seconds.toFixed(2) }} s from {{ noisePrint.source }}
          <span v-if="!printMatchesRate" class="text-destructive">
            ({{ noisePrint.sampleRate }} Hz; this file is {{ doc.sampleRate }} Hz)</span
          >
        </template>
        <template v-else>
          Select half a second or more of background noise with no speech, then capture it.
        </template>
      </p>
      <div class="flex items-center gap-2">
        <label :class="[hint, 'w-16']" for="cleanup-noise-db">Reduce by</label>
        <ScrubNumber
          id="cleanup-noise-db"
          v-model="settings.noiseReductionDb"
          :min="1"
          :max="40"
          unit="dB"
        />
      </div>
      <div class="flex items-center gap-2">
        <label :class="[hint, 'w-16']" for="cleanup-noise-strength">Strength</label>
        <ScrubNumber
          id="cleanup-noise-strength"
          v-model="settings.noiseStrength"
          :min="0.5"
          :max="4"
          :step="0.25"
          unit="×"
          title="Higher removes more noise but can make the voice sound watery"
        />
        <Button
          size="sm"
          variant="outline"
          class="ml-auto"
          :disabled="!noisePrint || !printMatchesRate"
          @click="apply('noise')"
          >Reduce noise</Button
        >
      </div>
    </CleanupSection>

    <CleanupSection
      title="Clicks"
      :previewing="previewKind === 'clicks'"
      @toggle-preview="togglePreview('clicks')"
    >
      <div class="flex items-center gap-2">
        <select v-model="settings.clickSensitivity" aria-label="Click sensitivity" :class="select">
          <option value="low">Low sensitivity</option>
          <option value="medium">Medium sensitivity</option>
          <option value="high">High sensitivity</option>
        </select>
        <Button size="sm" variant="outline" class="ml-auto" @click="apply('clicks')"
          >Remove clicks</Button
        >
      </div>
      <p :class="hint">
        Finds mouth clicks and ticks up to 2 ms long and rebuilds the waveform under them.
      </p>
    </CleanupSection>

    <CleanupSection
      title="De-esser"
      :previewing="previewKind === 'deEss'"
      @toggle-preview="togglePreview('deEss')"
    >
      <div class="grid grid-cols-[4rem_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5">
        <label :class="hint" for="cleanup-deess-hz">Above</label>
        <ScrubNumber
          id="cleanup-deess-hz"
          v-model="settings.deEssHz"
          :min="2000"
          :max="12000"
          log
          unit="Hz"
        />
        <label :class="hint" for="cleanup-deess-threshold">Threshold</label>
        <ScrubNumber
          id="cleanup-deess-threshold"
          v-model="settings.deEssThresholdDb"
          :max="0"
          unit="dB"
        />
        <label :class="hint" for="cleanup-deess-max">Max cut</label>
        <ScrubNumber
          id="cleanup-deess-max"
          v-model="settings.deEssMaxDb"
          :min="1"
          :max="30"
          unit="dB"
        />
      </div>
      <div class="flex items-center gap-2">
        <p :class="hint">
          <template v-if="sibilancePeak !== null">
            Sibilance peaks at {{ formatDb(sibilancePeak, 'dB', 0) }}
          </template>
          <template v-else-if="doc.channels">Select up to 2 min to measure sibilance</template>
        </p>
        <Button size="sm" variant="outline" class="ml-auto" @click="apply('deEss')">De-ess</Button>
      </div>
    </CleanupSection>

    <CleanupSection
      title="EQ"
      :previewing="previewKind === 'eq'"
      @toggle-preview="togglePreview('eq')"
    >
      <EqCurve
        v-model="settings.eq"
        :sample-rate="doc.sampleRate"
        :spectrum="spectrum"
        :names="EQ_NAMES"
      />
      <div
        class="grid grid-cols-[auto_minmax(0,1.2fr)_minmax(0,1fr)_minmax(0,0.8fr)] items-center gap-x-1.5 gap-y-1.5"
      >
        <span />
        <span :class="hint">Freq</span>
        <span :class="hint">Gain</span>
        <span :class="hint">Q</span>
        <template v-for="(band, index) in settings.eq" :key="index">
          <span :class="hint">{{ EQ_NAMES[index] }}</span>
          <ScrubNumber
            v-model="band.frequency"
            :label="`${EQ_NAMES[index]} frequency`"
            :min="20"
            :max="20000"
            log
            unit="Hz"
            fill
          />
          <ScrubNumber
            v-model="band.gainDb"
            :label="`${EQ_NAMES[index]} gain`"
            :min="-18"
            :max="18"
            :step="0.5"
            unit="dB"
            fill
          />
          <ScrubNumber
            v-if="band.type === 'peaking'"
            v-model="band.q"
            :label="`${EQ_NAMES[index]} Q`"
            :min="0.1"
            :max="10"
            :step="0.1"
            fill
          />
          <span v-else />
        </template>
      </div>
      <p :class="hint">
        Cut 250–500 Hz for a boxy room, 2–5 kHz for harshness; a high shelf adds air.
      </p>
      <Button size="sm" variant="outline" class="w-full" :disabled="eqIsFlat" @click="apply('eq')"
        >Apply EQ</Button
      >
    </CleanupSection>

    <CleanupSection
      title="Compressor"
      :previewing="previewKind === 'compress'"
      @toggle-preview="togglePreview('compress')"
    >
      <div
        class="grid grid-cols-[auto_minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5"
      >
        <label :class="hint" for="cleanup-comp-threshold">Threshold</label>
        <ScrubNumber
          id="cleanup-comp-threshold"
          v-model="settings.compThresholdDb"
          :max="0"
          unit="dB"
          fill
        />
        <label :class="hint" for="cleanup-comp-ratio">Ratio</label>
        <ScrubNumber
          id="cleanup-comp-ratio"
          v-model="settings.compRatio"
          :min="1"
          :max="20"
          :step="0.5"
          unit=":1"
          fill
        />
        <label :class="hint" for="cleanup-comp-attack">Attack</label>
        <ScrubNumber
          id="cleanup-comp-attack"
          v-model="settings.compAttackMs"
          :min="0"
          unit="ms"
          fill
        />
        <label :class="hint" for="cleanup-comp-release">Release</label>
        <ScrubNumber
          id="cleanup-comp-release"
          v-model="settings.compReleaseMs"
          :min="1"
          :step="10"
          unit="ms"
          fill
        />
        <label :class="hint" for="cleanup-comp-makeup">Makeup</label>
        <ScrubNumber
          id="cleanup-comp-makeup"
          v-model="settings.compMakeupDb"
          :step="0.5"
          unit="dB"
          fill
        />
      </div>
      <p v-if="compressorReadout" :class="hint">
        Peaks at {{ formatDb(compressorReadout.peakDb, 'dB', 0) }}, so up to
        {{ formatDb(compressorReadout.reductionDb, 'dB', 1) }} of gain reduction.
      </p>
      <Button size="sm" variant="outline" class="w-full" @click="apply('compress')"
        >Compress</Button
      >
    </CleanupSection>

    <section class="space-y-2 border-t pt-4">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Batch</h3>
      <p :class="hint">
        Runs the ticked steps, top to bottom, with the settings above over whole files. Each file
        gets one undo step; Save all writes them.
      </p>
      <div class="grid grid-cols-2 gap-x-2 gap-y-1">
        <label
          v-for="kind in CLEANUP_ORDER"
          :key="kind"
          class="flex items-center gap-2 text-xs"
          :class="batchBlocked(kind) ? 'text-muted-foreground' : ''"
          :title="batchBlocked(kind) ?? undefined"
        >
          <input
            type="checkbox"
            class="size-3.5 accent-primary"
            :checked="settings.batchSteps.includes(kind)"
            :disabled="!!batchBlocked(kind)"
            @change="setBatchStep(kind, ($event.target as HTMLInputElement).checked)"
          />
          {{ CLEANUP_NAMES[kind] }}
        </label>
      </div>
      <Button
        size="sm"
        class="w-full"
        :disabled="!!batchProgress || batchChain.length === 0 || !workspace.documents.length"
        @click="runBatch"
      >
        Run on {{ batchScopeLabel }}
      </Button>
      <div v-if="batchProgress" class="space-y-1" role="status">
        <div class="flex items-center gap-2 text-xs text-muted-foreground">
          <Loader2 class="size-3 animate-spin" />
          Cleaning {{ batchProgress.done }} / {{ batchProgress.total }}
        </div>
        <div class="h-1.5 overflow-hidden rounded-full bg-muted">
          <div
            class="h-full bg-primary transition-[width]"
            :style="{ width: `${(batchProgress.done / Math.max(1, batchProgress.total)) * 100}%` }"
          />
        </div>
      </div>
    </section>
  </div>
</template>
