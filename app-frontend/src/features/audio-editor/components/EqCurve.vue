<script setup lang="ts">
import { useResizeObserver } from '@vueuse/core'
import { computed, ref } from 'vue'
import { MAX_FREQUENCY, MIN_FREQUENCY, type AverageSpectrum } from '../lib/averageSpectrum'
import { eqResponseDb, type EqBand } from '../lib/filters'

/**
 * The EQ drawn over the file's average spectrum, like an analyzer EQ: drag a numbered handle to
 * move its band, scroll on a bell to change its width, double-click to reset its gain.
 */
const bands = defineModel<EqBand[]>({ required: true })
const props = defineProps<{
  sampleRate: number
  spectrum: AverageSpectrum | null
  names: string[]
}>()

const HEIGHT = 150
const PAD_TOP = 8
const PAD_BOTTOM = 16
const RANGE_DB = 18
/** How many dB of the spectrum fit the height, from its loudest point down. */
const SPECTRUM_SPAN_DB = 72
const GRID_FREQUENCIES = [50, 100, 200, 500, 1000, 2000, 5000, 10000]
const GRID_DB = [-12, -6, 6, 12]
const FREQUENCY_LABELS: Record<number, string> = { 100: '100', 1000: '1k', 10000: '10k' }

const svg = ref<SVGSVGElement | null>(null)
const width = ref(280)
useResizeObserver(svg, (entries) => {
  width.value = Math.max(120, entries[0]!.contentRect.width)
})

const top = computed(() => Math.min(MAX_FREQUENCY, props.sampleRate / 2))
const logSpan = computed(() => Math.log(top.value / MIN_FREQUENCY))

function xAt(frequency: number) {
  return (width.value * Math.log(frequency / MIN_FREQUENCY)) / logSpan.value
}
function frequencyAt(x: number) {
  return (
    MIN_FREQUENCY * Math.exp((Math.min(width.value, Math.max(0, x)) / width.value) * logSpan.value)
  )
}
const middle = (PAD_TOP + HEIGHT - PAD_BOTTOM) / 2
const halfHeight = (HEIGHT - PAD_BOTTOM - PAD_TOP) / 2
function yAt(db: number) {
  return middle - (db / RANGE_DB) * halfHeight
}
function dbAt(y: number) {
  return ((middle - y) / halfHeight) * RANGE_DB
}

const curvePath = computed(() => {
  const count = Math.max(2, Math.round(width.value / 2))
  const frequencies = Float32Array.from({ length: count }, (_, i) =>
    frequencyAt((i / (count - 1)) * width.value),
  )
  const response = eqResponseDb(bands.value, props.sampleRate, frequencies)
  return Array.from(frequencies, (frequency, i) => {
    const y = Math.min(HEIGHT, Math.max(0, yAt(response[i]!)))
    return `${i ? 'L' : 'M'}${xAt(frequency).toFixed(1)},${y.toFixed(1)}`
  }).join('')
})

const spectrumPath = computed(() => {
  const spectrum = props.spectrum
  if (!spectrum) return ''
  let loudest = -Infinity
  for (const level of spectrum.levelsDb) loudest = Math.max(loudest, level)
  if (!Number.isFinite(loudest) || loudest < -110) return ''
  const floor = HEIGHT - PAD_BOTTOM
  const points = Array.from(spectrum.frequencies, (frequency, i) => {
    const depth = Math.min(1, (loudest - spectrum.levelsDb[i]!) / SPECTRUM_SPAN_DB)
    return `L${xAt(frequency).toFixed(1)},${(PAD_TOP + depth * (floor - PAD_TOP)).toFixed(1)}`
  })
  return `M0,${floor}${points.join('')}L${width.value},${floor}Z`
})

const active = ref<number | null>(null)
let dragging: { index: number; pointerId: number } | null = null

function updateBand(index: number, change: Partial<EqBand>) {
  bands.value = bands.value.map((band, i) => (i === index ? { ...band, ...change } : band))
}

function roundFrequency(frequency: number) {
  return Number(frequency.toPrecision(frequency >= 1000 ? 4 : 3))
}

function onHandleDown(event: PointerEvent, index: number) {
  if (event.button !== 0) return
  event.preventDefault()
  ;(event.currentTarget as Element).setPointerCapture(event.pointerId)
  ;(event.currentTarget as SVGElement).focus()
  dragging = { index, pointerId: event.pointerId }
  active.value = index
}

function onHandleMove(event: PointerEvent) {
  if (!dragging || event.pointerId !== dragging.pointerId || !svg.value) return
  const rect = svg.value.getBoundingClientRect()
  const gainDb = Math.round(dbAt(event.clientY - rect.top) * 2) / 2
  updateBand(dragging.index, {
    frequency: roundFrequency(frequencyAt(event.clientX - rect.left)),
    gainDb: Math.max(-RANGE_DB, Math.min(RANGE_DB, gainDb)),
  })
}

function onHandleUp(event: PointerEvent) {
  if (!dragging || event.pointerId !== dragging.pointerId) return
  dragging = null
}

function changeQ(index: number, direction: number) {
  const band = bands.value[index]
  if (!band || band.type !== 'peaking') return
  const q = band.q * Math.pow(1.15, direction)
  updateBand(index, { q: Number(Math.min(10, Math.max(0.1, q)).toFixed(2)) })
}

function onWheel(event: WheelEvent, index: number) {
  if (bands.value[index]?.type !== 'peaking') return
  event.preventDefault()
  // Scrolling up narrows the bell.
  changeQ(index, event.deltaY < 0 ? 1 : -1)
}

function onKey(event: KeyboardEvent, index: number) {
  const band = bands.value[index]
  if (!band) return
  const coarse = event.shiftKey ? 4 : 1
  const keys: Record<string, () => void> = {
    ArrowUp: () => updateBand(index, { gainDb: Math.min(RANGE_DB, band.gainDb + 0.5 * coarse) }),
    ArrowDown: () => updateBand(index, { gainDb: Math.max(-RANGE_DB, band.gainDb - 0.5 * coarse) }),
    ArrowRight: () =>
      updateBand(index, {
        frequency: roundFrequency(Math.min(top.value, band.frequency * Math.pow(2, coarse / 12))),
      }),
    ArrowLeft: () =>
      updateBand(index, {
        frequency: roundFrequency(
          Math.max(MIN_FREQUENCY, band.frequency / Math.pow(2, coarse / 12)),
        ),
      }),
    PageUp: () => changeQ(index, 1),
    PageDown: () => changeQ(index, -1),
    '0': () => updateBand(index, { gainDb: 0 }),
  }
  const action = keys[event.key]
  if (!action) return
  // Keep the editor's own shortcuts (arrows switch files) out of it.
  event.preventDefault()
  event.stopPropagation()
  action()
}

function formatFrequency(frequency: number) {
  return frequency >= 1000
    ? `${Number((frequency / 1000).toFixed(2))} kHz`
    : `${Math.round(frequency)} Hz`
}

function describe(index: number) {
  const band = bands.value[index]
  if (!band) return ''
  const gain = `${band.gainDb > 0 ? '+' : band.gainDb < 0 ? '−' : ''}${Math.abs(band.gainDb).toFixed(1)} dB`
  const q = band.type === 'peaking' ? ` · Q ${band.q.toFixed(2)}` : ''
  return `${props.names[index]} · ${formatFrequency(band.frequency)} · ${gain}${q}`
}

const readout = computed(() =>
  active.value === null
    ? 'Drag a handle · scroll a bell to change its width'
    : describe(active.value),
)
</script>

<template>
  <div class="space-y-1">
    <svg
      ref="svg"
      class="block w-full touch-none select-none rounded-md bg-[#120d1b]"
      :height="HEIGHT"
      :viewBox="`0 0 ${width} ${HEIGHT}`"
      role="group"
      aria-label="EQ curve over the file's spectrum"
    >
      <path v-if="spectrumPath" :d="spectrumPath" fill="#3a2f52" fill-opacity="0.85" />
      <g stroke="#2c2240" stroke-width="1">
        <line
          v-for="frequency in GRID_FREQUENCIES"
          :key="frequency"
          :x1="xAt(frequency)"
          :x2="xAt(frequency)"
          :y1="0"
          :y2="HEIGHT - PAD_BOTTOM"
        />
        <line
          v-for="db in GRID_DB"
          :key="db"
          x1="0"
          :x2="width"
          :y1="yAt(db)"
          :y2="yAt(db)"
          stroke-dasharray="2 3"
        />
      </g>
      <line x1="0" :x2="width" :y1="yAt(0)" :y2="yAt(0)" stroke="#4a3d5e" />
      <g fill="#a99cba" font-size="9" font-family="ui-monospace, monospace">
        <text
          v-for="(label, frequency) in FREQUENCY_LABELS"
          :key="frequency"
          :x="xAt(Number(frequency))"
          :y="HEIGHT - 4"
          text-anchor="middle"
        >
          {{ label }}
        </text>
        <text x="3" :y="yAt(12) + 3">+12</text>
        <text x="3" :y="yAt(-12) + 3">−12</text>
      </g>
      <path :d="curvePath" fill="none" stroke="#e6c8f7" stroke-width="2" stroke-linejoin="round" />
      <g
        v-for="(band, index) in bands"
        :key="index"
        tabindex="0"
        role="slider"
        :aria-label="`${names[index]}: arrows move it, Page Up and Down change the width, 0 resets`"
        :aria-valuetext="describe(index)"
        :aria-valuenow="band.gainDb"
        :aria-valuemin="-RANGE_DB"
        :aria-valuemax="RANGE_DB"
        class="cursor-grab outline-none focus-visible:[&>circle]:stroke-[#ff6b9d]"
        :class="active === index ? 'cursor-grabbing' : ''"
        :transform="`translate(${xAt(band.frequency)},${yAt(band.gainDb)})`"
        @pointerdown="onHandleDown($event, index)"
        @pointermove="onHandleMove"
        @pointerup="onHandleUp"
        @pointercancel="onHandleUp"
        @pointerenter="active = index"
        @pointerleave="!dragging && (active = null)"
        @focus="active = index"
        @blur="active = null"
        @wheel="onWheel($event, index)"
        @dblclick="updateBand(index, { gainDb: 0 })"
        @keydown="onKey($event, index)"
      >
        <circle r="12" fill="transparent" />
        <circle
          :r="active === index ? 8 : 7"
          :fill="band.gainDb === 0 ? '#120d1b' : '#b77ddb'"
          stroke="#e6c8f7"
          stroke-width="1.5"
        />
        <text
          text-anchor="middle"
          dy="3.5"
          font-size="9"
          font-weight="600"
          :fill="band.gainDb === 0 ? '#e6c8f7' : '#120d1b'"
          class="pointer-events-none"
        >
          {{ index + 1 }}
        </text>
      </g>
    </svg>
    <p class="truncate text-xs tabular-nums text-muted-foreground" aria-live="polite">
      {{ readout }}
    </p>
  </div>
</template>
