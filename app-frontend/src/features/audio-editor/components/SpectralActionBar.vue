<script setup lang="ts">
import { Bandage, Eraser, FilePlus2, Headphones, Volume2, X } from 'lucide-vue-next'
import { computed, onBeforeUnmount, ref, watch, watchEffect } from 'vue'
import { toast } from 'vue-sonner'
import { describeStep, SPECTRAL_NAMES, stepProblem } from '../lib/cleanupSteps'
import type { SpectralMode } from '../lib/spectral'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'
import ScrubNumber from './ScrubNumber.vue'

/**
 * What can be done with a spectral selection, floating next to it on the spectrogram: heal, delete,
 * change its level, or listen to it alone. Each but delete can be previewed first (B flips to the
 * original), and the spectrogram shows the previewed audio, so an edit is seen and heard before it
 * is applied.
 */
const props = defineProps<{ doc: EditorDocument }>()

const workspace = useAudioWorkspace()
const settings = computed(() => workspace.spectralSettings)
const selection = computed(() => props.doc.spectralSelection)

// One computed per mode, so a new gain does not re-render a heal preview.
const steps = {
  heal: computed(() => workspace.spectralStep('heal', selection.value)),
  gain: computed(() => workspace.spectralStep('gain', selection.value)),
  isolate: computed(() => workspace.spectralStep('isolate', selection.value)),
}
type PreviewMode = keyof typeof steps

const healProblem = computed(() => {
  const step = steps.heal.value
  return step ? stepProblem(step, props.doc.sampleRate) : null
})

const previewMode = ref<PreviewMode | null>(null)
watchEffect(() => {
  const step = previewMode.value ? steps[previewMode.value].value : null
  if (step) workspace.preview = stepProblem(step, props.doc.sampleRate) ? null : step
})
watch(previewMode, (mode, previous) => {
  workspace.previewBypass = false
  if (!mode && previous) workspace.preview = null
})
// Another preview (a Cleanup step) took over: this one is off.
watch(
  () => workspace.preview,
  (step) => {
    if (previewMode.value && step !== steps[previewMode.value].value) previewMode.value = null
  },
)
onBeforeUnmount(() => {
  if (!previewMode.value) return
  workspace.preview = null
  workspace.previewBypass = false
})

function togglePreview(mode: PreviewMode) {
  previewMode.value = previewMode.value === mode ? null : mode
}

function reportError(error: unknown, fallback: string) {
  toast.error(error instanceof Error ? error.message : fallback)
}

/** Applies the edit; the previewed render is reused when it is of this very edit. */
async function apply(mode: Exclude<SpectralMode, 'isolate'>) {
  const doc = props.doc
  try {
    if (mode === 'delete') {
      await workspace.applySpectral(doc, 'delete')
      return
    }
    const step = steps[mode].value
    if (!step) return
    const problem = stepProblem(step, doc.sampleRate)
    if (problem) throw new Error(problem)
    await workspace.applyStep(doc, step, () => describeStep(step))
    // It is in the audio now; previewing it on top would apply it twice.
    if (previewMode.value === mode) previewMode.value = null
  } catch (error) {
    reportError(error, `${SPECTRAL_NAMES[mode]} failed.`)
  }
}

async function extract() {
  try {
    await workspace.extractSpectral(props.doc)
    toast.success('Extracted the selection into a new file.')
  } catch (error) {
    reportError(error, 'The selection could not be extracted.')
  }
}

const button =
  'inline-flex h-7 items-center gap-1 rounded px-2 text-xs font-medium transition-colors disabled:opacity-40 [&_svg]:size-3.5'
const plain = 'text-[#e8e2f2] hover:bg-white/10'
const on = 'bg-[#8ee3ff] text-[#0c1225]'
const divider = 'mx-0.5 h-5 w-px bg-white/15'
</script>

<template>
  <div
    class="flex items-center gap-0.5 rounded-lg border border-white/15 bg-[#120d1b]/90 p-1 text-[#e8e2f2] shadow-lg backdrop-blur"
    role="toolbar"
    aria-label="Spectral selection"
  >
    <button
      type="button"
      :class="[button, previewMode === 'heal' ? on : plain, 'px-1.5']"
      :aria-pressed="previewMode === 'heal'"
      :disabled="!!healProblem"
      aria-label="Preview heal"
      :title="
        healProblem ?? 'Preview heal: hear and see it before applying (B flips to the original)'
      "
      @click="togglePreview('heal')"
    >
      <Headphones />
    </button>
    <button
      type="button"
      :class="[button, plain]"
      :disabled="!!healProblem"
      :title="
        healProblem ??
        'Heal (Ctrl+U): bring the area down to the sound around it, for coughs, clicks, squeaks and beeps'
      "
      @click="apply('heal')"
    >
      <Bandage /> Heal
    </button>
    <span :class="divider" />
    <button
      type="button"
      :class="[button, plain]"
      title="Delete (Del): silence the selected area"
      @click="apply('delete')"
    >
      <Eraser /> Delete
    </button>
    <span :class="divider" />
    <div class="w-[4.75rem] text-foreground">
      <ScrubNumber
        v-model="settings.gainDb"
        label="Spectral gain"
        :min="-60"
        :max="24"
        :step="0.5"
        unit="dB"
        fill
      />
    </div>
    <button
      type="button"
      :class="[button, previewMode === 'gain' ? on : plain, 'px-1.5']"
      :aria-pressed="previewMode === 'gain'"
      aria-label="Preview gain"
      title="Preview the gain change (B flips to the original)"
      @click="togglePreview('gain')"
    >
      <Headphones />
    </button>
    <button
      type="button"
      :class="[button, plain]"
      :disabled="settings.gainDb === 0"
      title="Turn the selected area up or down by this much"
      @click="apply('gain')"
    >
      <Volume2 /> Gain
    </button>
    <span :class="divider" />
    <button
      type="button"
      :class="[button, previewMode === 'isolate' ? on : plain]"
      :aria-pressed="previewMode === 'isolate'"
      title="Solo: hear just the selected area (B flips to the original)"
      @click="togglePreview('isolate')"
    >
      <Headphones /> Solo
    </button>
    <button
      type="button"
      :class="[button, plain, 'px-1.5']"
      aria-label="Extract to a new file"
      title="Extract: copy just the selected area, everything else silenced, into a new file"
      @click="extract"
    >
      <FilePlus2 />
    </button>
    <span :class="divider" />
    <button
      type="button"
      :class="[button, plain, 'px-1.5']"
      aria-label="Clear the spectral selection"
      title="Clear the selection (Esc)"
      @click="workspace.setSpectralSelection(doc, null)"
    >
      <X />
    </button>
  </div>
</template>
