<script setup lang="ts">
import { ref } from 'vue'

/**
 * A number field that can also be dragged, like the knobs in a DAW: drag up or right to raise it,
 * down or left to lower it. A click without dragging focuses it for typing. `log` makes a drag
 * move by ratios (an octave per 100 px), which suits frequencies.
 */
const model = defineModel<number>({ required: true })
const props = withDefaults(
  defineProps<{
    min?: number
    max?: number
    step?: number
    log?: boolean
    unit?: string
    id?: string
    label?: string
    title?: string
    /** Stretch to the column instead of the usual fixed width. */
    fill?: boolean
  }>(),
  { step: 1 },
)

const input = ref<HTMLInputElement | null>(null)
const PIXELS_PER_STEP = 4
const PIXELS_PER_OCTAVE = 100
const DRAG_THRESHOLD = 3

let drag: { pointerId: number; x: number; y: number; value: number; moved: boolean } | null = null

function clamp(value: number) {
  return Math.min(props.max ?? Infinity, Math.max(props.min ?? -Infinity, value))
}

function decimals(step: number) {
  const text = String(step)
  return text.includes('.') ? text.length - text.indexOf('.') - 1 : 0
}

function round(value: number) {
  if (props.log) return Number(value.toPrecision(value >= 1000 ? 4 : 3))
  const stepped = Math.round(value / props.step) * props.step
  return Number(stepped.toFixed(decimals(props.step)))
}

function onPointerDown(event: PointerEvent) {
  // Once focused, pointer presses place the caret and select text as usual.
  if (event.pointerType !== 'mouse' || event.button !== 0) return
  if (document.activeElement === input.value) return
  event.preventDefault()
  input.value?.setPointerCapture(event.pointerId)
  drag = {
    pointerId: event.pointerId,
    x: event.clientX,
    y: event.clientY,
    value: model.value,
    moved: false,
  }
}

function onPointerMove(event: PointerEvent) {
  if (!drag || event.pointerId !== drag.pointerId) return
  const pixels = event.clientX - drag.x - (event.clientY - drag.y)
  if (!drag.moved && Math.abs(pixels) < DRAG_THRESHOLD) return
  drag.moved = true
  const next = props.log
    ? Math.max(1e-6, drag.value) * Math.pow(2, pixels / PIXELS_PER_OCTAVE)
    : drag.value + Math.round(pixels / PIXELS_PER_STEP) * props.step
  model.value = clamp(round(next))
}

function onPointerUp(event: PointerEvent) {
  if (!drag || event.pointerId !== drag.pointerId) return
  const moved = drag.moved
  drag = null
  input.value?.releasePointerCapture(event.pointerId)
  if (!moved) {
    input.value?.focus()
    input.value?.select()
  }
}

/** Typing updates as it goes but only clamps once done, so typing "5" on the way to "50" works. */
function onInput(event: Event) {
  const value = (event.target as HTMLInputElement).valueAsNumber
  if (Number.isFinite(value)) model.value = value
}

function onChange(event: Event) {
  const target = event.target as HTMLInputElement
  const value = target.valueAsNumber
  model.value = Number.isFinite(value) ? clamp(value) : model.value
  target.value = String(model.value)
}
</script>

<template>
  <div class="relative" :class="fill ? 'w-full min-w-0' : 'w-20 shrink-0'">
    <input
      :id="id"
      ref="input"
      :value="model"
      type="number"
      :min="min"
      :max="max"
      :step="step"
      :aria-label="label"
      :title="title ?? 'Drag up or down to change, or click to type'"
      class="h-8 w-full min-w-0 cursor-ns-resize rounded-md border bg-background px-2 text-sm tabular-nums outline-none [appearance:textfield] focus:cursor-text focus-visible:ring-2 focus-visible:ring-ring/50 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
      :class="unit ? 'pr-7' : ''"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @input="onInput"
      @change="onChange"
    />
    <span
      v-if="unit"
      class="pointer-events-none absolute inset-y-0 right-2 flex items-center text-xs text-muted-foreground"
      aria-hidden="true"
      >{{ unit }}</span
    >
  </div>
</template>
