<script setup lang="ts">
import { useResizeObserver } from '@vueuse/core'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { PreviewDisplay } from '../composables/usePreviewDisplay'
import { pickLevel } from '../lib/peaks'
import type { Spectrogram } from '../lib/spectrogram'
import type { EditorDocument } from '../stores/workspace'
import { formatTime } from '../lib/format'

const props = defineProps<{
  doc: EditorDocument
  /** Playhead in frames while playing. */
  playhead: number | null
  /** Which displays to show; with both on, the spectrogram sits under the waveform. */
  views: { waveform: boolean; spectral: boolean }
  /** Frames a trim would keep; everything outside is shaded as about to be cut. */
  cutPreview?: { start: number; end: number } | null
  /** Audio a cleanup preview would produce, drawn in place of the file's own. */
  preview?: PreviewDisplay | null
  /** Says which version is on screen while a preview is on. */
  previewLabel?: string | null
}>()

/** The audio to draw: the preview's while there is one, else the file's. */
const shown = computed(() => {
  const preview = props.preview?.docId === props.doc.id ? props.preview : null
  return {
    channels: preview?.channels ?? props.doc.channels,
    peaks: preview?.peaks ?? props.doc.peaks,
    spectrogram: preview?.spectrogram ?? props.doc.spectrogram,
  }
})

const emit = defineEmits<{
  select: [selection: { start: number; end: number } | null, cursor: number]
  view: [view: { start: number; samplesPerPixel: number }]
}>()

const RULER = 22
const OVERVIEW = 18
const EDGE_GRAB = 6
const MIN_SAMPLES_PER_PIXEL = 1 / 32

const colors = {
  background: '#120d1b',
  lane: '#17111f',
  laneLine: '#2c2240',
  grid: '#221a31',
  wave: '#b77ddb',
  waveSelected: '#e6c8f7',
  selection: 'rgba(255, 255, 255, 0.13)',
  ruler: '#1c1527',
  rulerText: '#a99cba',
  rulerTick: '#4a3d5e',
  cursor: '#ff6b9d',
  playhead: '#fbd866',
  overview: '#5d4479',
  overviewWindow: 'rgba(251, 216, 102, 0.22)',
  cut: 'rgba(255, 70, 110, 0.18)',
  cutHatch: 'rgba(255, 107, 157, 0.35)',
}

const container = ref<HTMLDivElement | null>(null)
const canvas = ref<HTMLCanvasElement | null>(null)
const size = ref({ width: 0, height: 0 })

useResizeObserver(container, (entries) => {
  const rect = entries[0]!.contentRect
  size.value = { width: Math.floor(rect.width), height: Math.floor(rect.height) }
})

const lanesTop = RULER
const lanesHeight = computed(() => Math.max(0, size.value.height - RULER - OVERVIEW))
const SPLIT_GAP = 3

interface Region {
  top: number
  height: number
}

/** The waveform takes the top half and the spectrogram the bottom half when both are shown. */
const regions = computed<{ waveform: Region | null; spectral: Region | null }>(() => {
  const { waveform, spectral } = props.views
  const full = { top: lanesTop, height: lanesHeight.value }
  if (!waveform || !spectral)
    return { waveform: waveform ? full : null, spectral: spectral ? full : null }
  const waveHeight = Math.round((lanesHeight.value - SPLIT_GAP) / 2)
  return {
    waveform: { top: lanesTop, height: waveHeight },
    spectral: {
      top: lanesTop + waveHeight + SPLIT_GAP,
      height: lanesHeight.value - waveHeight - SPLIT_GAP,
    },
  }
})

function fitView() {
  const width = Math.max(1, size.value.width)
  return { start: 0, samplesPerPixel: Math.max(MIN_SAMPLES_PER_PIXEL, props.doc.frames / width) }
}

const view = computed(() => props.doc.view ?? fitView())

function clampView(start: number, samplesPerPixel: number) {
  const width = Math.max(1, size.value.width)
  const maxSpp = Math.max(MIN_SAMPLES_PER_PIXEL, props.doc.frames / width)
  const spp = Math.min(maxSpp, Math.max(MIN_SAMPLES_PER_PIXEL, samplesPerPixel))
  const maxStart = Math.max(0, props.doc.frames - width * spp)
  return { start: Math.min(maxStart, Math.max(0, start)), samplesPerPixel: spp }
}

function setView(start: number, samplesPerPixel: number) {
  emit('view', clampView(start, samplesPerPixel))
}

function frameAt(x: number) {
  return view.value.start + x * view.value.samplesPerPixel
}

function xAt(frame: number) {
  return (frame - view.value.start) / view.value.samplesPerPixel
}

function zoomAround(x: number, factor: number) {
  const anchor = frameAt(x)
  const spp = view.value.samplesPerPixel * factor
  setView(anchor - x * spp, spp)
}

function zoomIn() {
  const focus = props.doc.selection
    ? xAt((props.doc.selection.start + props.doc.selection.end) / 2)
    : xAt(props.doc.cursor)
  zoomAround(Math.min(size.value.width, Math.max(0, focus)), 1 / 2)
}

function zoomOut() {
  zoomAround(size.value.width / 2, 2)
}

function zoomToFit() {
  emit('view', fitView())
}

function zoomToSelection() {
  const selection = props.doc.selection
  if (!selection) return
  const length = selection.end - selection.start
  const width = Math.max(1, size.value.width)
  setView(selection.start - length * 0.05, (length * 1.1) / width)
}

defineExpose({ zoomIn, zoomOut, zoomToFit, zoomToSelection })

// Keep the playhead on screen while playing, turning the page like Audition does.
watch(
  () => props.playhead,
  (playhead) => {
    if (playhead === null || props.doc.view === null) return
    const visible = size.value.width * view.value.samplesPerPixel
    if (playhead < view.value.start || playhead > view.value.start + visible)
      setView(playhead - visible * 0.05, view.value.samplesPerPixel)
  },
)

const spectrogramImages = new WeakMap<Spectrogram, HTMLCanvasElement>()
const spectrumColors = [
  [12, 18, 37],
  [28, 47, 90],
  [37, 93, 146],
  [58, 158, 189],
  [154, 214, 203],
  [255, 220, 139],
] as const

function spectrogramImage(spectrogram: Spectrogram) {
  let image = spectrogramImages.get(spectrogram)
  if (image) return image
  image = document.createElement('canvas')
  image.width = spectrogram.columns
  image.height = spectrogram.rows
  const context = image.getContext('2d')!
  const pixels = context.createImageData(spectrogram.columns, spectrogram.rows)
  const palette = Array.from({ length: 256 }, (_, value) => {
    const scaled = (value / 255) * (spectrumColors.length - 1)
    const index = Math.min(spectrumColors.length - 2, Math.floor(scaled))
    const fraction = scaled - index
    return [0, 1, 2].map((c) =>
      Math.round(
        spectrumColors[index]![c]! * (1 - fraction) + spectrumColors[index + 1]![c]! * fraction,
      ),
    )
  })
  for (let i = 0; i < spectrogram.intensity.length; i++) {
    const [red, green, blue] = palette[spectrogram.intensity[i]!]!
    pixels.data[i * 4] = red!
    pixels.data[i * 4 + 1] = green!
    pixels.data[i * 4 + 2] = blue!
    pixels.data[i * 4 + 3] = 255
  }
  context.putImageData(pixels, 0, 0)
  spectrogramImages.set(spectrogram, image)
  return image
}

const TICK_STEPS = [0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60]

function drawRuler(context: CanvasRenderingContext2D, width: number) {
  const { sampleRate } = props.doc
  context.fillStyle = colors.ruler
  context.fillRect(0, 0, width, RULER)
  if (!sampleRate) return
  const pixelsPerSecond = sampleRate / view.value.samplesPerPixel
  const step = TICK_STEPS.find((candidate) => candidate * pixelsPerSecond >= 80) ?? 120
  const first = Math.floor(view.value.start / sampleRate / step) * step
  const last = frameAt(width) / sampleRate
  context.font = '10px ui-monospace, SFMono-Regular, Menlo, monospace'
  context.textBaseline = 'middle'
  for (let time = first; time <= last + step; time += step) {
    const x = Math.round(xAt(time * sampleRate)) + 0.5
    if (x < -50 || x > width + 50) continue
    context.fillStyle = colors.rulerTick
    context.fillRect(x, RULER - 8, 1, 8)
    for (let minor = 1; minor < 5; minor++) {
      const minorX = Math.round(xAt((time + (step * minor) / 5) * sampleRate)) + 0.5
      context.fillRect(minorX, RULER - 4, 1, 4)
    }
    context.fillStyle = colors.rulerText
    context.fillText(formatTime(time, step < 0.01 ? 3 : step < 1 ? 2 : 0), x + 3, RULER / 2 - 2)
  }
}

function drawWaveform(context: CanvasRenderingContext2D, width: number, region: Region) {
  const { doc } = props
  const { channels, peaks: pyramid } = shown.value
  const lanes = Math.max(1, doc.channelCount)
  const laneHeight = region.height / lanes
  const spp = view.value.samplesPerPixel
  const level = pyramid ? pickLevel(pyramid, spp) : null
  const selection = doc.selection

  for (let lane = 0; lane < lanes; lane++) {
    const top = region.top + lane * laneHeight
    const mid = top + laneHeight / 2
    const scale = (laneHeight / 2) * 0.92
    context.fillStyle = colors.lane
    context.fillRect(0, top, width, laneHeight)
    context.fillStyle = colors.grid
    context.fillRect(0, Math.round(mid - scale / 2), width, 1)
    context.fillRect(0, Math.round(mid + scale / 2), width, 1)
    context.fillStyle = colors.laneLine
    context.fillRect(0, Math.round(mid), width, 1)
    if (lane > 0) context.fillRect(0, Math.round(top), width, 1)

    const samples = channels?.[lane]
    const peaks = level?.channels[lane]
    if (!samples && !peaks) continue

    const colorAt = (x: number) => {
      if (!selection) return colors.wave
      const frame = frameAt(x)
      return frame >= selection.start && frame < selection.end ? colors.waveSelected : colors.wave
    }

    if (spp >= 1 || !samples) {
      for (let x = 0; x < width; x++) {
        const from = frameAt(x)
        const to = from + spp
        let min = Infinity,
          max = -Infinity
        if (level && peaks) {
          const bucket = level.samplesPerBucket
          const end = Math.min(peaks.length / 2, Math.ceil(to / bucket))
          for (let b = Math.max(0, Math.floor(from / bucket)); b < end; b++) {
            min = Math.min(min, peaks[b * 2]!)
            max = Math.max(max, peaks[b * 2 + 1]!)
          }
        } else if (samples) {
          const end = Math.min(samples.length, Math.ceil(to))
          for (let i = Math.max(0, Math.floor(from)); i < end; i++) {
            min = Math.min(min, samples[i]!)
            max = Math.max(max, samples[i]!)
          }
        }
        if (min > max) continue
        context.fillStyle = colorAt(x)
        const y = mid - max * scale
        context.fillRect(x, y, 1, Math.max(1, (max - min) * scale))
      }
    } else {
      // Zoomed past one sample per pixel: join the samples and mark each one.
      const first = Math.max(0, Math.floor(view.value.start) - 1)
      const last = Math.min(samples.length - 1, Math.ceil(frameAt(width)) + 1)
      context.strokeStyle = colors.wave
      context.lineWidth = 1.25
      context.beginPath()
      for (let i = first; i <= last; i++) {
        const x = xAt(i)
        const y = mid - samples[i]! * scale
        if (i === first) context.moveTo(x, y)
        else context.lineTo(x, y)
      }
      context.stroke()
      if (spp < 0.25) {
        context.fillStyle = colors.waveSelected
        for (let i = first; i <= last; i++)
          context.fillRect(xAt(i) - 1.5, mid - samples[i]! * scale - 1.5, 3, 3)
      }
    }
  }
}

function drawSpectral(context: CanvasRenderingContext2D, width: number, region: Region) {
  const spectrogram = shown.value.spectrogram
  context.fillStyle = '#0c1225'
  context.fillRect(0, region.top, width, region.height)
  if (!spectrogram) return
  const image = spectrogramImage(spectrogram)
  const sourceX = view.value.start / spectrogram.hop
  const sourceWidth = (width * view.value.samplesPerPixel) / spectrogram.hop
  context.imageSmoothingEnabled = sourceWidth < width
  context.drawImage(
    image,
    sourceX,
    0,
    sourceWidth,
    spectrogram.rows,
    0,
    region.top,
    width,
    region.height,
  )
}

function drawOverview(context: CanvasRenderingContext2D, width: number, height: number) {
  const top = height - OVERVIEW
  context.fillStyle = colors.ruler
  context.fillRect(0, top, width, OVERVIEW)
  const { doc } = props
  const pyramid = shown.value.peaks
  const coarse = pyramid?.[pyramid.length - 1]
  if (coarse && doc.frames > 0) {
    const perPixel = doc.frames / width
    context.fillStyle = colors.overview
    const mid = top + OVERVIEW / 2
    for (let x = 0; x < width; x++) {
      let peak = 0
      const end = Math.ceil(((x + 1) * perPixel) / coarse.samplesPerBucket)
      for (let b = Math.floor((x * perPixel) / coarse.samplesPerBucket); b < end; b++)
        for (const channel of coarse.channels)
          peak = Math.max(peak, Math.abs(channel[b * 2] ?? 0), Math.abs(channel[b * 2 + 1] ?? 0))
      const half = peak * (OVERVIEW / 2 - 2)
      context.fillRect(x, mid - half, 1, Math.max(1, half * 2))
    }
    const windowStart = (view.value.start / doc.frames) * width
    const windowWidth = ((width * view.value.samplesPerPixel) / doc.frames) * width
    context.fillStyle = colors.overviewWindow
    context.fillRect(windowStart, top, Math.max(2, windowWidth), OVERVIEW)
  }
}

let pendingFrame = 0

function draw() {
  pendingFrame = 0
  const element = canvas.value
  if (!element) return
  const { width, height } = size.value
  const ratio = window.devicePixelRatio || 1
  if (element.width !== width * ratio || element.height !== height * ratio) {
    element.width = width * ratio
    element.height = height * ratio
  }
  const context = element.getContext('2d')
  if (!context || width === 0 || height === 0) return
  context.setTransform(ratio, 0, 0, ratio, 0, 0)
  context.fillStyle = colors.background
  context.fillRect(0, 0, width, height)

  const { waveform, spectral } = regions.value
  if (waveform) drawWaveform(context, width, waveform)
  if (spectral) drawSpectral(context, width, spectral)
  if (waveform && spectral) {
    context.fillStyle = colors.ruler
    context.fillRect(0, waveform.top + waveform.height, width, SPLIT_GAP)
  }

  const selection = props.doc.selection
  if (selection) {
    const left = Math.max(0, xAt(selection.start))
    const right = Math.min(width, xAt(selection.end))
    if (right > left) {
      context.fillStyle = colors.selection
      context.fillRect(left, lanesTop, right - left, lanesHeight.value)
      context.fillStyle = 'rgba(255,255,255,0.35)'
      context.fillRect(left, 0, right - left, 3)
    }
  }

  const preview = props.cutPreview
  if (preview) {
    context.save()
    context.beginPath()
    const cutLeft = Math.min(width, xAt(preview.start))
    const cutRight = Math.max(0, xAt(preview.end))
    if (cutLeft > 0) context.rect(0, lanesTop, cutLeft, lanesHeight.value)
    if (cutRight < width) context.rect(cutRight, lanesTop, width - cutRight, lanesHeight.value)
    context.clip()
    context.fillStyle = colors.cut
    context.fillRect(0, lanesTop, width, lanesHeight.value)
    // Diagonal hatching so the shading reads as "removed", not as another selection.
    context.strokeStyle = colors.cutHatch
    context.lineWidth = 1
    context.beginPath()
    for (let x = -lanesHeight.value; x < width; x += 8) {
      context.moveTo(x, lanesTop + lanesHeight.value)
      context.lineTo(x + lanesHeight.value, lanesTop)
    }
    context.stroke()
    context.restore()
  }

  drawRuler(context, width)

  const cursorX = Math.round(xAt(props.doc.cursor)) + 0.5
  if (cursorX >= 0 && cursorX <= width) {
    context.fillStyle = colors.cursor
    context.fillRect(cursorX - 0.5, 0, 1, height - OVERVIEW)
    context.beginPath()
    context.moveTo(cursorX - 5, 0)
    context.lineTo(cursorX + 5, 0)
    context.lineTo(cursorX, 7)
    context.fill()
  }

  if (props.playhead !== null) {
    const x = Math.round(xAt(props.playhead)) + 0.5
    context.fillStyle = colors.playhead
    context.fillRect(x - 0.5, 0, 1.5, height - OVERVIEW)
  }

  drawOverview(context, width, height)
}

function scheduleDraw() {
  if (!pendingFrame) pendingFrame = requestAnimationFrame(draw)
}

watch(
  () => [
    props.doc.id,
    props.doc.state,
    props.doc.peaks,
    props.doc.channels,
    props.doc.spectrogram,
    props.doc.selection,
    props.doc.cursor,
    props.doc.view,
    props.playhead,
    props.views.waveform,
    props.views.spectral,
    props.cutPreview?.start,
    props.cutPreview?.end,
    shown.value,
    size.value,
  ],
  scheduleDraw,
  { immediate: true },
)

onBeforeUnmount(() => cancelAnimationFrame(pendingFrame))

type Drag =
  | { kind: 'select'; anchor: number; downX: number; moved: boolean; extend: boolean }
  | { kind: 'edge'; fixed: number }
  | { kind: 'overview' }
  | { kind: 'ruler' }

let drag: Drag | null = null
const hoverCursor = ref('text')

function localPoint(event: PointerEvent | WheelEvent | MouseEvent) {
  const rect = canvas.value!.getBoundingClientRect()
  return { x: event.clientX - rect.left, y: event.clientY - rect.top }
}

function clampFrame(frame: number) {
  return Math.round(Math.max(0, Math.min(props.doc.frames, frame)))
}

function edgeAt(x: number) {
  const selection = props.doc.selection
  if (!selection) return null
  if (Math.abs(x - xAt(selection.start)) <= EDGE_GRAB) return selection.end
  if (Math.abs(x - xAt(selection.end)) <= EDGE_GRAB) return selection.start
  return null
}

function panOverview(x: number) {
  const visible = size.value.width * view.value.samplesPerPixel
  const center = (x / Math.max(1, size.value.width)) * props.doc.frames
  setView(center - visible / 2, view.value.samplesPerPixel)
}

function onPointerDown(event: PointerEvent) {
  if (event.button !== 0 || !props.doc.frames) return
  canvas.value!.setPointerCapture(event.pointerId)
  const { x, y } = localPoint(event)
  if (y >= size.value.height - OVERVIEW) {
    drag = { kind: 'overview' }
    panOverview(x)
    return
  }
  if (y < RULER) {
    drag = { kind: 'ruler' }
    emit('select', props.doc.selection, clampFrame(frameAt(x)))
    return
  }
  const fixed = edgeAt(x)
  if (fixed !== null) {
    drag = { kind: 'edge', fixed }
    return
  }
  const anchor = event.shiftKey
    ? (props.doc.selection?.start ?? props.doc.cursor)
    : clampFrame(frameAt(x))
  drag = { kind: 'select', anchor, downX: x, moved: false, extend: event.shiftKey }
  if (event.shiftKey) updateSelection(anchor, clampFrame(frameAt(x)))
}

function updateSelection(a: number, b: number) {
  const start = Math.min(a, b)
  const end = Math.max(a, b)
  emit('select', end > start ? { start, end } : null, start)
}

function onPointerMove(event: PointerEvent) {
  const { x, y } = localPoint(event)
  if (!drag) {
    hoverCursor.value =
      y >= size.value.height - OVERVIEW
        ? 'grab'
        : y < RULER
          ? 'pointer'
          : edgeAt(x) !== null
            ? 'col-resize'
            : 'text'
    return
  }
  const frame = clampFrame(frameAt(Math.max(0, Math.min(size.value.width, x))))
  if (drag.kind === 'overview') panOverview(x)
  else if (drag.kind === 'ruler') emit('select', props.doc.selection, frame)
  else if (drag.kind === 'edge') updateSelection(drag.fixed, frame)
  else {
    if (!drag.moved && Math.abs(x - drag.downX) < 3) return
    drag.moved = true
    updateSelection(drag.anchor, frame)
  }
}

function onPointerUp(event: PointerEvent) {
  if (drag?.kind === 'select' && !drag.moved && !drag.extend) {
    emit('select', null, clampFrame(frameAt(localPoint(event).x)))
  }
  drag = null
}

function onDoubleClick() {
  if (props.doc.frames) emit('select', { start: 0, end: props.doc.frames }, 0)
}

function onWheel(event: WheelEvent) {
  if (!props.doc.frames) return
  const { x } = localPoint(event)
  const horizontal = event.shiftKey ? event.deltaY : event.deltaX
  if (Math.abs(horizontal) > Math.abs(event.deltaY) || event.shiftKey) {
    setView(view.value.start + horizontal * view.value.samplesPerPixel, view.value.samplesPerPixel)
  } else {
    zoomAround(x, Math.pow(1.0015, event.deltaY))
  }
}
</script>

<template>
  <div
    ref="container"
    class="relative min-h-0 flex-1 overflow-hidden rounded-md border bg-[#120d1b]"
  >
    <canvas
      ref="canvas"
      class="absolute inset-0 block h-full w-full touch-none select-none"
      :style="{ cursor: hoverCursor }"
      role="img"
      :aria-label="`Waveform of ${doc.name}, ${formatTime(doc.frames / (doc.sampleRate || 1), 2)} long`"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @dblclick="onDoubleClick"
      @wheel.prevent="onWheel"
    />
    <div
      v-if="previewLabel && doc.status === 'ready'"
      class="pointer-events-none absolute right-2 rounded bg-black/60 px-2 py-0.5 text-xs font-medium text-[#e6c8f7]"
      :style="{ top: `${RULER + 6}px` }"
    >
      {{ previewLabel }}
    </div>
    <div
      v-if="doc.status === 'loading'"
      class="absolute inset-0 grid place-items-center text-sm text-[#a99cba]"
    >
      Decoding…
    </div>
    <div
      v-else-if="doc.status === 'error'"
      class="absolute inset-0 grid place-items-center px-6 text-center text-sm text-[#ff9db8]"
    >
      {{ doc.error ?? 'This file could not be opened.' }}
    </div>
    <div
      v-else-if="regions.spectral && !shown.spectrogram"
      class="pointer-events-none absolute inset-x-0 grid place-items-center text-sm text-[#a99cba]"
      :style="{ top: `${regions.spectral.top}px`, height: `${regions.spectral.height}px` }"
    >
      Building spectrogram…
    </div>
  </div>
</template>
