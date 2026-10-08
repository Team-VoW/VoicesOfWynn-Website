<script setup lang="ts">
import { computed } from 'vue'
import { formatDb, formatSignedDb } from '../lib/format'
import { planMatch } from '../lib/loudness'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

// Its own component so each finished analysis re-renders one row rather than the whole table.
const props = defineProps<{ doc: EditorDocument; active: boolean }>()
const emit = defineEmits<{ select: [] }>()

const workspace = useAudioWorkspace()

const plan = computed(() =>
  props.doc.analysis
    ? planMatch(props.doc.analysis, workspace.targetLufs, workspace.ceilingDbtp)
    : null,
)

const limiterNote = computed(() => {
  const value = plan.value
  if (!value?.needsLimiting) return undefined
  return `Limiter needed: ${formatSignedDb(value.gainDb)} would put the true peak at ${formatDb(value.peakAfterDbtp, 'dBTP')}, over the ${workspace.ceilingDbtp} dBTP max. The limiter holds the peaks down, so this file ends up slightly more compressed.`
})
</script>

<template>
  <tr
    class="cursor-pointer border-t hover:bg-accent"
    :class="active ? 'bg-primary/5' : ''"
    @click="emit('select')"
  >
    <td class="max-w-28 truncate px-2 py-1" :title="doc.path">{{ doc.name }}</td>
    <td class="px-2 py-1 text-right">
      {{ formatDb(doc.analysis?.integratedLufs, '').trim() }}
    </td>
    <td class="px-2 py-1 text-right">
      {{ formatDb(doc.analysis?.truePeakDbtp, '').trim() }}
    </td>
    <td
      class="px-2 py-1 text-right"
      :class="plan?.needsLimiting ? 'text-amber-700 dark:text-amber-400' : ''"
      :title="limiterNote"
    >
      <template v-if="plan">
        {{ formatSignedDb(plan.gainDb) }}{{ plan.needsLimiting ? ' ⚑' : '' }}
      </template>
      <template v-else>—</template>
    </td>
  </tr>
</template>
