<script setup lang="ts">
import { AlertTriangle, Loader2, X } from 'lucide-vue-next'
import { computed } from 'vue'
import {
  LEADING_SILENCE_LIMIT,
  TRAILING_SILENCE_LIMIT,
  TRUE_PEAK_WARNING,
  validateAudioFileName,
} from '@/features/tools/lib/audioChecks'
import { formatDb } from '../lib/format'
import { isDirty, type EditorDocument } from '../stores/workspace'

// One component per row so a file finishing its analysis re-renders only its own row, not all
// 650 of them.
const props = defineProps<{ doc: EditorDocument; active: boolean }>()
const emit = defineEmits<{
  select: []
  check: [checked: boolean]
  close: []
}>()

const folder = computed(() => {
  const index = props.doc.path.lastIndexOf('/')
  return index > 0 ? props.doc.path.slice(0, index) : ''
})

interface Problem {
  /** A few words, shown under the open file's name. */
  label: string
  /** The full explanation, in the tooltip. */
  detail: string
}

/** What the VoW delivery rules (the Audio check page) would fail about this file. */
const problems = computed(() => {
  const { doc } = props
  const list: Problem[] = []
  const nameError = validateAudioFileName(doc.name)
  if (nameError) list.push({ label: 'File name', detail: nameError })
  const analysis = doc.analysis
  if (analysis) {
    if (analysis.truePeakDbtp > TRUE_PEAK_WARNING)
      list.push({
        label: `Peak ${formatDb(analysis.truePeakDbtp, 'dBTP', 2)}`,
        detail: `True peak ${formatDb(analysis.truePeakDbtp, 'dBTP', 2)} is above ${TRUE_PEAK_WARNING} dBTP.`,
      })
    if (analysis.leadingSilenceSeconds > LEADING_SILENCE_LIMIT)
      list.push({
        label: `Lead ${analysis.leadingSilenceSeconds.toFixed(2)} s`,
        detail: `Leading silence ${analysis.leadingSilenceSeconds.toFixed(2)} s is over ${LEADING_SILENCE_LIMIT} s.`,
      })
    if (analysis.trailingSilenceSeconds > TRAILING_SILENCE_LIMIT)
      list.push({
        label: `Tail ${analysis.trailingSilenceSeconds.toFixed(2)} s`,
        detail: `Trailing silence ${analysis.trailingSilenceSeconds.toFixed(2)} s is over ${TRAILING_SILENCE_LIMIT} s.`,
      })
    if (doc.channelCount > 1)
      list.push({ label: 'Stereo', detail: 'Stereo; voice lines ship mono.' })
  }
  return list
})

const problemSummary = computed(() =>
  ['Breaks the VoW audio rules:', ...problems.value.map((problem) => `• ${problem.detail}`)].join(
    '\n',
  ),
)
</script>

<template>
  <li
    :id="`audio-file-${doc.id}`"
    role="option"
    :aria-selected="active"
    class="group flex cursor-pointer items-start gap-2 rounded-md px-2 py-1 text-sm"
    :class="active ? 'bg-primary/10 ring-1 ring-primary/30' : 'hover:bg-accent'"
    @click="emit('select')"
  >
    <input
      :checked="doc.checked"
      type="checkbox"
      class="mt-[3px] size-3.5 shrink-0 accent-primary"
      :aria-label="`Tick ${doc.name} for batch actions`"
      @click.stop
      @change="emit('check', ($event.target as HTMLInputElement).checked)"
    />
    <div class="min-w-0 flex-1">
      <div class="flex items-center gap-1.5">
        <span
          v-if="isDirty(doc)"
          class="size-2 shrink-0 rounded-full bg-[#ff6b9d]"
          title="Unsaved changes"
        />
        <span class="truncate font-medium" :title="doc.path">{{ doc.name }}</span>
        <span
          v-if="doc.status === 'error'"
          class="ml-auto inline-flex shrink-0"
          :title="doc.error ?? 'Could not open'"
          :aria-label="doc.error ?? 'Could not open'"
          role="img"
        >
          <AlertTriangle class="size-3 text-destructive" aria-hidden="true" />
        </span>
        <Loader2
          v-else-if="doc.status === 'loading' || (doc.analyzing && !doc.analysis)"
          class="ml-auto size-3 shrink-0 animate-spin text-muted-foreground"
          aria-label="Analyzing"
        />
        <span
          v-else-if="problems.length && !active"
          class="ml-auto inline-flex shrink-0"
          :title="problemSummary"
          :aria-label="problemSummary"
          role="img"
        >
          <AlertTriangle class="size-3 text-amber-600" aria-hidden="true" />
        </span>
      </div>
      <p
        v-if="active && problems.length"
        class="mt-0.5 flex flex-wrap gap-x-2 text-[11px] text-amber-700 dark:text-amber-400"
        :title="problemSummary"
      >
        <span
          v-for="problem in problems"
          :key="problem.label"
          class="inline-flex items-center gap-1"
        >
          <AlertTriangle class="size-3" aria-hidden="true" />{{ problem.label }}
        </span>
      </p>
      <div v-if="folder" class="truncate text-[11px] text-muted-foreground">
        {{ folder }}
      </div>
    </div>
    <button
      type="button"
      class="rounded p-0.5 text-muted-foreground opacity-0 transition-opacity hover:bg-background hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100"
      :aria-label="`Close ${doc.name}`"
      @click.stop="emit('close')"
    >
      <X class="size-3.5" />
    </button>
  </li>
</template>
