<script setup lang="ts">
import {
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { computed, ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { formatTime, parseTime } from '../lib/format'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

/** Audition's Marker Properties: name, description and exact times of one marker. */
const props = defineProps<{ doc: EditorDocument | null }>()
const markerId = defineModel<number | null>({ required: true })

const workspace = useAudioWorkspace()
const marker = computed(
  () => props.doc?.markers.find((entry) => entry.id === markerId.value) ?? null,
)
const open = computed({
  get: () => !!marker.value,
  set: (value) => {
    if (!value) markerId.value = null
  },
})

const form = ref({ name: '', comment: '', start: '', end: '' })
/** The times as first shown: shown to the millisecond, so an untouched one keeps its exact frame. */
let shown = { start: '', end: '' }
const seconds = (frames: number) => formatTime(frames / (props.doc?.sampleRate || 1))

watch(
  marker,
  (current, previous) => {
    if (!current || current.id === previous?.id) return
    form.value = {
      name: current.name,
      comment: current.comment,
      start: seconds(current.start),
      end: seconds(current.start + current.length),
    }
    shown = { start: form.value.start, end: form.value.end }
  },
  { immediate: true },
)

const isRange = computed(() => (marker.value?.length ?? 0) > 0)

/** The typed times as frames, or why they cannot be used. */
const times = computed(() => {
  const doc = props.doc
  const current = marker.value
  if (!doc || !current) return { error: '' }
  const toFrame = (text: string, original: string, exact: number) => {
    if (text === original) return exact
    const value = parseTime(text)
    return value === null ? null : Math.round(value * doc.sampleRate)
  }
  const start = toFrame(form.value.start, shown.start, current.start)
  if (start === null) return { error: 'Start is not a time, like 1:23.456.' }
  if (start > doc.frames) return { error: `Start is past the end (${seconds(doc.frames)}).` }
  if (!isRange.value) return { start, length: 0 }
  const end = toFrame(form.value.end, shown.end, current.start + current.length)
  if (end === null) return { error: 'End is not a time, like 1:23.456.' }
  if (end > doc.frames) return { error: `End is past the end (${seconds(doc.frames)}).` }
  if (end <= start) return { error: 'End has to come after the start.' }
  return { start, length: end - start }
})

function save() {
  const doc = props.doc
  const current = marker.value
  const value = times.value
  if (!doc || !current || 'error' in value) return
  const patch = {
    name: form.value.name,
    comment: form.value.comment,
    start: value.start,
    length: value.length,
  }
  const changed = (Object.keys(patch) as (keyof typeof patch)[]).some(
    (key) => patch[key] !== current[key],
  )
  if (changed) workspace.updateMarker(doc, current.id, patch, 'Edit marker')
  markerId.value = null
}

const field =
  'h-8 w-full rounded-md border bg-background px-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50'
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/50" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 rounded-lg border bg-card p-5 text-card-foreground shadow-xl focus:outline-none"
      >
        <form class="space-y-4" @submit.prevent="save">
          <div class="space-y-1">
            <DialogTitle class="text-base font-semibold">Marker properties</DialogTitle>
            <DialogDescription class="text-sm text-muted-foreground">
              {{ isRange ? 'Range marker' : 'Point marker' }}. Saved into the WAV with the file.
            </DialogDescription>
          </div>
          <label class="block space-y-1 text-xs font-medium">
            Name
            <input v-model="form.name" :class="field" autocomplete="off" />
          </label>
          <label class="block space-y-1 text-xs font-medium">
            Description
            <textarea
              v-model="form.comment"
              rows="3"
              class="w-full rounded-md border bg-background px-2 py-1.5 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
              placeholder="The line's text, a take note…"
            />
          </label>
          <div class="grid gap-3" :class="isRange ? 'grid-cols-2' : 'grid-cols-1'">
            <label class="space-y-1 text-xs font-medium">
              {{ isRange ? 'Start' : 'Position' }}
              <input v-model="form.start" :class="field" class="font-mono tabular-nums" />
            </label>
            <label v-if="isRange" class="space-y-1 text-xs font-medium">
              End
              <input v-model="form.end" :class="field" class="font-mono tabular-nums" />
            </label>
          </div>
          <p v-if="'error' in times" class="text-xs text-destructive">{{ times.error }}</p>
          <p v-else-if="isRange" class="text-xs tabular-nums text-muted-foreground">
            Length {{ seconds(times.length) }}
          </p>
          <div class="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" @click="markerId = null">Cancel</Button>
            <Button type="submit" size="sm" :disabled="'error' in times">Save</Button>
          </div>
        </form>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
