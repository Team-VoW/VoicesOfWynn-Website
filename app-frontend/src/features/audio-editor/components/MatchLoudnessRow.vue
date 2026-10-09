<script setup lang="ts">
import { computed } from 'vue'
import { formatDb, formatSignedDb } from '../lib/format'
import { LIMITER_BUDGET_DB, planMatch } from '../lib/matchLoudness'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

// Its own component so each finished analysis re-renders one row rather than the whole table.
const props = defineProps<{ doc: EditorDocument; active: boolean }>()
const emit = defineEmits<{ select: [] }>()

const workspace = useAudioWorkspace()

const plan = computed(() =>
  props.doc.analysis
    ? planMatch(props.doc.analysis, {
        targetLufs: workspace.targetLufs,
        ceilingDbtp: workspace.ceilingDbtp,
        peakControl: workspace.peakControl,
      })
    : null,
)

const peakNote = computed(() => {
  const value = plan.value
  if (!value?.needsLimiting) return undefined
  const over = `${formatSignedDb(value.gainDb)} would put the true peak at ${formatDb(value.peakAfterDbtp, 'dBTP')}, ${value.reductionDb.toFixed(1)} dB over the ${workspace.ceilingDbtp} dBTP max.`
  if (value.compresses)
    return `${over} Auto eases the loud syllables down with a gentle compressor, then the limiter trims what is left.`
  if (value.reductionDb <= LIMITER_BUDGET_DB)
    return `${over} The limiter only touches the odd peak, which is transparent at this amount.`
  return `${over} The limiter holds the peaks down; this much can flatten the loud syllables. Auto would compress them gently instead.`
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
      :title="peakNote"
    >
      <template v-if="plan">
        {{ formatSignedDb(plan.gainDb) }}{{ plan.needsLimiting ? ' ⚑' : '' }}
      </template>
      <template v-else>—</template>
    </td>
  </tr>
</template>
