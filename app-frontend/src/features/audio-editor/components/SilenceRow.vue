<script setup lang="ts">
import { AlertTriangle } from 'lucide-vue-next'
import { computed } from 'vue'
import { LEADING_SILENCE_LIMIT, TRAILING_SILENCE_LIMIT } from '@/features/tools/lib/audioChecks'
import { formatDb } from '../lib/format'
import { edgeSoundAboveSilenceRule, explainLoudEdges } from '../lib/silence'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

// Its own component so each finished analysis re-renders one row rather than the whole table.
const props = defineProps<{ doc: EditorDocument; active: boolean }>()
const emit = defineEmits<{ select: [] }>()

const workspace = useAudioWorkspace()

const analysis = computed(() => props.doc.analysis)
const leadOver = computed(
  () => !!analysis.value && analysis.value.leadingSilenceSeconds > LEADING_SILENCE_LIMIT,
)
const tailOver = computed(
  () => !!analysis.value && analysis.value.trailingSilenceSeconds > TRAILING_SILENCE_LIMIT,
)
const edgeSound = computed(
  () => !!analysis.value && edgeSoundAboveSilenceRule(analysis.value.silence),
)

const warn = 'text-amber-700 dark:text-amber-400'
</script>

<template>
  <tr
    class="cursor-pointer border-t hover:bg-accent"
    :class="active ? 'bg-primary/5' : ''"
    @click="emit('select')"
  >
    <td class="max-w-28 truncate px-2 py-1" :title="doc.path">{{ doc.name }}</td>
    <template v-if="analysis">
      <td class="px-2 py-1 text-right" :class="leadOver ? warn : ''">
        {{ analysis.leadingSilenceSeconds.toFixed(2) }} s
      </td>
      <td class="px-2 py-1 text-right" :class="tailOver ? warn : ''">
        {{ analysis.trailingSilenceSeconds.toFixed(2) }} s
      </td>
      <td
        class="px-2 py-1 text-right"
        :class="edgeSound ? warn : ''"
        :title="edgeSound ? explainLoudEdges(analysis.silence) : undefined"
      >
        <span class="inline-flex items-center gap-1">
          <AlertTriangle v-if="edgeSound" class="size-3" aria-hidden="true" />
          {{ formatDb(workspace.trimThresholdFor(doc), '', 0).trim() }}
        </span>
      </td>
    </template>
    <td
      v-else-if="doc.status === 'error'"
      colspan="3"
      class="px-2 py-1 text-right text-destructive"
    >
      error
    </td>
    <td v-else colspan="3" class="px-2 py-1 text-right text-muted-foreground">…</td>
  </tr>
</template>
