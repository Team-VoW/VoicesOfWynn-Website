<script setup lang="ts">
import { watchDebounced } from '@vueuse/core'
import { computed, ref } from 'vue'
import {
  LEADING_SILENCE_LIMIT,
  TRAILING_SILENCE_LIMIT,
  TRUE_PEAK_WARNING,
} from '@/features/tools/lib/audioChecks'
import { runAnalysisJob } from '../lib/analysisClient'
import { formatDb, formatTime } from '../lib/format'
import type { AudioAnalysis } from '../lib/loudness'
import type { EditorDocument } from '../stores/workspace'

const props = defineProps<{ doc: EditorDocument; playhead: number | null }>()

const selectionAnalysis = ref<AudioAnalysis | null>(null)

const seconds = (frames: number) => frames / (props.doc.sampleRate || 1)

const format = computed(() => {
  const { doc } = props
  if (!doc.sampleRate) return ''
  const channels =
    doc.channelCount === 1 ? 'Mono' : doc.channelCount === 2 ? 'Stereo' : `${doc.channelCount} ch`
  const depth = doc.layout
    ? `${doc.layout.bitDepth}-bit${doc.layout.format === 'float' ? ' float' : ''}`
    : ''
  return [`${doc.sampleRate} Hz`, channels, depth].filter(Boolean).join(' · ')
})

// Loudness of just the selection, like Audition's Amplitude Statistics on a range.
watchDebounced(
  () => [props.doc.id, props.doc.state, props.doc.selection?.start, props.doc.selection?.end],
  async () => {
    const { doc } = props
    const selection = doc.selection
    selectionAnalysis.value = null
    if (!selection || !doc.channels || selection.end - selection.start < doc.sampleRate * 0.4)
      return
    const state = doc.state
    const result = await runAnalysisJob({
      kind: 'analyze',
      channels: doc.channels.map((channel) => channel.slice(selection.start, selection.end)),
      sampleRate: doc.sampleRate,
    })
    if (doc.state === state && doc.selection === selection) selectionAnalysis.value = result
  },
  { debounce: 250, immediate: true },
)

const warn = 'text-destructive font-medium'
</script>

<template>
  <div
    class="flex flex-wrap items-center gap-x-5 gap-y-1 rounded-md border bg-card px-3 py-1.5 text-xs tabular-nums text-muted-foreground"
  >
    <span>
      <span class="text-foreground">{{ formatTime(seconds(playhead ?? doc.cursor)) }}</span>
      / {{ formatTime(seconds(doc.frames)) }}
    </span>
    <span v-if="doc.selection">
      Selection
      <span class="text-foreground">{{ formatTime(seconds(doc.selection.start)) }}</span>
      →
      <span class="text-foreground">{{ formatTime(seconds(doc.selection.end)) }}</span>
      ({{ formatTime(seconds(doc.selection.end - doc.selection.start)) }})
      <template v-if="selectionAnalysis">
        · {{ formatDb(selectionAnalysis.integratedLufs, 'LUFS') }} ·
        {{ formatDb(selectionAnalysis.truePeakDbtp, 'dBTP') }}
      </template>
    </span>
    <template v-if="doc.analysis">
      <span>
        File
        <span class="text-foreground">{{ formatDb(doc.analysis.integratedLufs, 'LUFS') }}</span>
      </span>
      <span :class="doc.analysis.truePeakDbtp > TRUE_PEAK_WARNING ? warn : ''">
        TP {{ formatDb(doc.analysis.truePeakDbtp, 'dBTP', 2) }}
      </span>
      <span :class="doc.analysis.leadingSilenceSeconds > LEADING_SILENCE_LIMIT ? warn : ''">
        Lead {{ doc.analysis.leadingSilenceSeconds.toFixed(3) }} s
      </span>
      <span :class="doc.analysis.trailingSilenceSeconds > TRAILING_SILENCE_LIMIT ? warn : ''">
        Tail {{ doc.analysis.trailingSilenceSeconds.toFixed(3) }} s
      </span>
    </template>
    <span v-else-if="doc.analyzing">Measuring…</span>
    <span class="ml-auto">{{ format }}</span>
  </div>
</template>
