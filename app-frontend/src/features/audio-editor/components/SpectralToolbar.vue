<script setup lang="ts">
import { Bandage, Lasso, Paintbrush, Rows3, SquareDashed, TextCursor } from 'lucide-vue-next'
import type { SpectralTool } from '../stores/workspace'

/** What a drag on the spectral display does, like the tool palette in Audition or RX. */
const tool = defineModel<SpectralTool>({ required: true })

const TOOLS = [
  { id: 'time', icon: TextCursor, label: 'Time selection', key: 'T' },
  { id: 'marquee', icon: SquareDashed, label: 'Marquee: a box of time and frequency', key: 'E' },
  { id: 'lasso', icon: Lasso, label: 'Lasso: draw around an area', key: 'D' },
  { id: 'brush', icon: Paintbrush, label: 'Brush: paint an area', key: 'P' },
  {
    id: 'frequency',
    icon: Rows3,
    label: 'Frequency band: a range of frequencies over the whole file',
    key: 'F',
  },
  {
    id: 'heal',
    icon: Bandage,
    label: 'Spot healing brush: paint over a noise to heal it',
    key: 'H',
  },
] as const
</script>

<template>
  <div
    class="inline-flex rounded-md border bg-background p-0.5"
    role="group"
    aria-label="Spectral tools"
  >
    <button
      v-for="option in TOOLS"
      :key="option.id"
      type="button"
      :aria-pressed="tool === option.id"
      :aria-label="`${option.label} (${option.key})`"
      :title="`${option.label} (${option.key})${option.id === 'time' || option.id === 'heal' ? '' : '\nShift adds to the selection, Alt cuts out of it'}`"
      class="grid size-7 place-items-center rounded [&_svg]:size-4"
      :class="
        tool === option.id
          ? 'bg-primary text-primary-foreground'
          : 'text-muted-foreground hover:text-foreground'
      "
      @click="tool = option.id"
    >
      <component :is="option.icon" />
    </button>
  </div>
</template>
