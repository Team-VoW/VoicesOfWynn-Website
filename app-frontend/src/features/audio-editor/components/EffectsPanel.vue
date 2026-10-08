<script setup lang="ts">
import { useLocalStorage } from '@vueuse/core'
import { computed } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import {
  crop,
  deleteRange,
  fade,
  gain,
  insertSilence,
  normalizePeak,
  silenceRange,
  toMono,
  type Channels,
  type FadeCurve,
} from '../lib/operations'
import { useAudioWorkspace, type EditorDocument, type EditResult } from '../stores/workspace'

const props = defineProps<{ doc: EditorDocument }>()

const workspace = useAudioWorkspace()
const settings = useLocalStorage('vow.audioEditor.effects', {
  gainDb: 3,
  normalizeDb: -1,
  fadeMs: 30,
  fadeCurve: 'linear' as FadeCurve,
  silenceMs: 100,
})
const mixPaste = computed(() => workspace.mixPasteSettings)
const clipboard = computed(() => workspace.clipboard)
const clipboardSeconds = computed(() =>
  clipboard.value ? (clipboard.value.channels[0]?.length ?? 0) / clipboard.value.sampleRate : 0,
)

const hasSelection = computed(() => !!props.doc.selection)
const scope = computed(() => (hasSelection.value ? 'selection' : 'whole file'))

async function run(
  label: string,
  edit: (channels: Channels, doc: EditorDocument) => EditResult | Channels,
) {
  try {
    await workspace.apply(props.doc, label, edit)
  } catch (error) {
    toast.error(error instanceof Error ? error.message : `${label} failed.`)
  }
}

async function clipboardEdit(task: (doc: EditorDocument) => Promise<void>, label: string) {
  try {
    await task(props.doc)
  } catch (error) {
    toast.error(error instanceof Error ? error.message : `${label} failed.`)
  }
}

function msToFrames(ms: number) {
  return Math.round((ms / 1000) * props.doc.sampleRate)
}

function applyGain(db: number) {
  void run(`Gain ${db > 0 ? '+' : ''}${db} dB`, (channels, doc) =>
    gain(channels, db, doc.selection),
  )
}

function applyFade(direction: 'in' | 'out') {
  void run(`Fade ${direction}`, (channels, doc) => {
    const length = Math.min(doc.frames, msToFrames(settings.value.fadeMs))
    const range =
      doc.selection ??
      (direction === 'in'
        ? { start: 0, end: length }
        : { start: doc.frames - length, end: doc.frames })
    return fade(channels, range, direction, settings.value.fadeCurve)
  })
}

function deleteSelection() {
  const selection = props.doc.selection
  if (!selection) return
  void run('Delete', (channels) => ({
    channels: deleteRange(channels, selection),
    selection: null,
    cursor: selection.start,
  }))
}

function cropToSelection() {
  const selection = props.doc.selection
  if (!selection) return
  void run('Crop', (channels) => ({
    channels: crop(channels, selection),
    selection: null,
    cursor: 0,
  }))
}

function silenceSelection() {
  const selection = props.doc.selection
  if (!selection) return
  void run('Silence', (channels) => silenceRange(channels, selection))
}

function addSilence() {
  const frames = msToFrames(settings.value.silenceMs)
  void run(`Insert ${settings.value.silenceMs} ms silence`, (channels, doc) => ({
    channels: insertSilence(channels, doc.cursor, frames),
    selection: { start: doc.cursor, end: doc.cursor + frames },
  }))
}

const field =
  'h-8 w-20 rounded-md border bg-background px-2 text-sm tabular-nums outline-none focus-visible:ring-2 focus-visible:ring-ring/50'
</script>

<template>
  <div class="space-y-5 text-sm">
    <p class="text-xs text-muted-foreground">
      Effects apply to the <span class="font-medium text-foreground">{{ scope }}</span> and can be
      undone with Ctrl+Z.
    </p>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Edit</h3>
      <div class="grid grid-cols-3 gap-2">
        <Button
          size="sm"
          variant="outline"
          :disabled="!hasSelection"
          title="Cut (Ctrl+X)"
          @click="clipboardEdit(workspace.cut, 'Cut')"
          >Cut</Button
        >
        <Button
          size="sm"
          variant="outline"
          :title="hasSelection ? 'Copy (Ctrl+C)' : 'Copy the whole file (Ctrl+C)'"
          @click="clipboardEdit(workspace.copy, 'Copy')"
          >Copy</Button
        >
        <Button
          size="sm"
          variant="outline"
          :disabled="!clipboard"
          :title="hasSelection ? 'Replace the selection (Ctrl+V)' : 'Insert at the cursor (Ctrl+V)'"
          @click="clipboardEdit(workspace.paste, 'Paste')"
          >Paste</Button
        >
        <Button size="sm" variant="outline" :disabled="!hasSelection" @click="deleteSelection"
          >Delete</Button
        >
        <Button size="sm" variant="outline" :disabled="!hasSelection" @click="cropToSelection"
          >Crop</Button
        >
        <Button size="sm" variant="outline" :disabled="!hasSelection" @click="silenceSelection"
          >Silence</Button
        >
      </div>
      <p class="text-xs text-muted-foreground">
        <template v-if="clipboard">
          Clipboard: {{ clipboardSeconds.toFixed(3) }} s from {{ clipboard.source }}
        </template>
        <template v-else>Clipboard is empty. Copy works across files.</template>
      </p>
      <div class="flex items-center gap-2">
        <select
          v-model="mixPaste.mode"
          aria-label="Mix paste mode"
          class="h-8 rounded-md border bg-background px-2 text-sm"
        >
          <option value="overlap">Overlap</option>
          <option value="overwrite">Overwrite</option>
        </select>
        <label class="sr-only" for="mix-volume">Pasted volume in dB</label>
        <input
          id="mix-volume"
          v-model.number="mixPaste.volumeDb"
          type="number"
          step="1"
          :class="field"
          title="Volume of the pasted audio"
        />
        <span class="text-xs text-muted-foreground">dB</span>
      </div>
      <div class="flex items-center gap-2">
        <label class="text-xs text-muted-foreground" for="mix-crossfade">Crossfade</label>
        <input
          id="mix-crossfade"
          v-model.number="mixPaste.crossfadeMs"
          type="number"
          min="0"
          step="5"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">ms</span>
      </div>
      <div class="grid grid-cols-2 gap-2">
        <Button
          size="sm"
          variant="outline"
          :disabled="!clipboard"
          title="Mix paste at the cursor (Ctrl+Shift+V)"
          @click="clipboardEdit(workspace.mixPaste, 'Mix paste')"
          >Mix paste</Button
        >
        <Button
          size="sm"
          variant="outline"
          title="Copy to a new file (Ctrl+Alt+C)"
          @click="clipboardEdit(workspace.copyToNew, 'Copy to new')"
          >Copy to new</Button
        >
      </div>
    </section>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Zero crossings
      </h3>
      <div class="grid grid-cols-2 gap-2">
        <Button
          size="sm"
          variant="outline"
          :disabled="!hasSelection"
          title="Shift+I"
          @click="workspace.adjustSelectionToZeroCrossings(doc, 'inward')"
          >Adjust inward</Button
        >
        <Button
          size="sm"
          variant="outline"
          :disabled="!hasSelection"
          title="Shift+O"
          @click="workspace.adjustSelectionToZeroCrossings(doc, 'outward')"
          >Adjust outward</Button
        >
      </div>
      <label class="flex items-center gap-2 text-xs text-muted-foreground">
        <input
          v-model="workspace.snapToZeroCrossings"
          type="checkbox"
          class="size-3.5 accent-primary"
        />
        Snap selections to zero crossings
      </label>
      <p class="text-xs text-muted-foreground">
        Cutting where the waveform crosses zero avoids clicks at the join.
      </p>
    </section>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Amplitude</h3>
      <div class="flex items-center gap-2">
        <label class="sr-only" for="effect-gain">Gain in dB</label>
        <input
          id="effect-gain"
          v-model.number="settings.gainDb"
          type="number"
          step="0.5"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">dB</span>
        <Button size="sm" class="ml-auto" @click="applyGain(settings.gainDb)">Apply gain</Button>
      </div>
      <div class="grid grid-cols-3 gap-2">
        <Button size="sm" variant="outline" title="Speaking → whispering" @click="applyGain(-5)"
          >−5 dB</Button
        >
        <Button size="sm" variant="outline" title="Speaking → shouting" @click="applyGain(5)"
          >+5 dB</Button
        >
        <Button size="sm" variant="outline" title="Whispering → shouting" @click="applyGain(10)"
          >+10 dB</Button
        >
      </div>
      <div class="flex items-center gap-2">
        <label class="sr-only" for="effect-normalize">Normalize peak to dBFS</label>
        <input
          id="effect-normalize"
          v-model.number="settings.normalizeDb"
          type="number"
          step="0.5"
          max="0"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">dBFS</span>
        <Button
          size="sm"
          variant="outline"
          class="ml-auto"
          @click="
            run(`Normalize to ${settings.normalizeDb} dBFS`, (channels, doc) =>
              normalizePeak(channels, settings.normalizeDb, doc.selection),
            )
          "
          >Normalize peak</Button
        >
      </div>
    </section>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Fades</h3>
      <div class="flex items-center gap-2">
        <label class="sr-only" for="effect-fade"
          >Fade length in milliseconds when nothing is selected</label
        >
        <input
          id="effect-fade"
          v-model.number="settings.fadeMs"
          type="number"
          min="1"
          step="5"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">ms</span>
        <select
          v-model="settings.fadeCurve"
          aria-label="Fade curve"
          class="ml-auto h-8 rounded-md border bg-background px-2 text-sm"
        >
          <option value="linear">Linear</option>
          <option value="logarithmic">Logarithmic</option>
        </select>
      </div>
      <div class="grid grid-cols-2 gap-2">
        <Button size="sm" variant="outline" @click="applyFade('in')">Fade in</Button>
        <Button size="sm" variant="outline" @click="applyFade('out')">Fade out</Button>
      </div>
      <p class="text-xs text-muted-foreground">
        Without a selection, fades the first or last {{ settings.fadeMs }} ms.
      </p>
    </section>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Insert silence
      </h3>
      <div class="flex items-center gap-2">
        <label class="sr-only" for="effect-insert">Silence to insert in milliseconds</label>
        <input
          id="effect-insert"
          v-model.number="settings.silenceMs"
          type="number"
          min="1"
          step="10"
          :class="field"
        />
        <span class="text-xs text-muted-foreground">ms</span>
        <Button size="sm" variant="outline" class="ml-auto" @click="addSilence"
          >Insert at cursor</Button
        >
      </div>
    </section>

    <section class="space-y-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Channels</h3>
      <Button
        size="sm"
        variant="outline"
        class="w-full"
        :disabled="doc.channelCount < 2"
        @click="run('Convert to mono', (channels) => toMono(channels))"
        >Convert to mono</Button
      >
    </section>
  </div>
</template>
