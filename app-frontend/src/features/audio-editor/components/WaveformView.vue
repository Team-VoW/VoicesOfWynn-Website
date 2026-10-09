<script setup lang="ts">
import { useElementSize, useLocalStorage, useResizeObserver } from '@vueuse/core'
import { Loader2 } from 'lucide-vue-next'
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import type { PreviewDisplay } from '../composables/usePreviewDisplay'
import { useViewSpectrogram } from '../composables/useViewSpectrogram'
import type { Marker } from '../lib/markers'
import MarkerMenu from './MarkerMenu.vue'
import SpectralActionBar from './SpectralActionBar.vue'
import SpectralSettings from './SpectralSettings.vue'
import { pickLevel } from '../lib/peaks'
import {
  axisFrequency,
  axisLabels,
  axisPosition,
  formatFrequency,
  fullAxis,
  panAxis,
  resolveAxis,
  warp,
  zoomAxis,
  type FrequencyAxis,
} from '../lib/frequencyScale'
import {
  extendStroke,
  selectionBounds,
  selectionIntervals,
  subtractIntervals,
  type Intervals,
  type SpectralPoint,
  type SpectralSelection,
  type SpectralShape,
} from '../lib/spectral'
import { intensityDb, type Spectrogram } from '../lib/spectrogram'
import { spectrumBackground, spectrumTable } from '../lib/spectrumColors'
import { useAudioWorkspace, type EditorDocument, type SpectralTool } from '../stores/workspace'
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
  /** What the editor is working on, shown with a spinner over the waveform. */
  busy?: string | null
}>()

/** The audio to draw: the preview's while there is one, else the file's. */
const shown = computed(() => {
  const preview = props.preview?.docId === props.doc.id ? props.preview : null
  return {
    channels: preview?.channels ?? props.doc.channels,
    peaks: preview?.peaks ?? props.doc.peaks,
  }
})

const emit = defineEmits<{
  select: [selection: { start: number; end: number } | null, cursor: number]
  view: [view: { start: number; samplesPerPixel: number }]
  markerSelect: [id: number]
  /** A marker drag is done: record `markers` as one step that undoes back to `before`. */
  markersCommit: [label: string, markers: Marker[], before: Marker[]]
  /** A marquee, lasso, brush or band drag on the spectral display is done; null clears it. */
  spectralSelect: [selection: SpectralSelection | null]
  /** A stroke of the spot healing brush, to heal right away. */
  spotHeal: [selection: SpectralSelection]
}>()

const workspace = useAudioWorkspace()

const RULER = 22
const MARKER_LANE = 16
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
  markerLane: '#150f1e',
  marker: '#56c2e6',
  markerFill: 'rgba(86, 194, 230, 0.28)',
  markerText: '#d7f3fc',
  markerSelected: '#8ee3ff',
  markerSelectedFill: 'rgba(142, 227, 255, 0.5)',
  frequencyText: 'rgba(214, 226, 245, 0.75)',
  spectralFill: 'rgba(255, 244, 214, 0.17)',
  spectralEdge: 'rgba(255, 244, 214, 0.95)',
}

const container = ref<HTMLDivElement | null>(null)
const canvas = ref<HTMLCanvasElement | null>(null)
const size = ref({ width: 0, height: 0 })
/** Where the marker being dragged is now. */
const liveMarker = shallowRef<Marker | null>(null)
/** The spectral selection as the drag in progress would leave it, and the lasso being drawn. */
const liveSpectral = shallowRef<{
  selection: SpectralSelection | null
  outline: { x: number; y: number }[] | null
} | null>(null)

useResizeObserver(container, (entries) => {
  const rect = entries[0]!.contentRect
  size.value = { width: Math.floor(rect.width), height: Math.floor(rect.height) }
})

const markerTop = RULER
const lanesTop = RULER + MARKER_LANE
const lanesHeight = computed(() => Math.max(0, size.value.height - lanesTop - OVERVIEW))
const SPLIT_GAP = 7
/** Neither display is squeezed below this by the divider. */
const MIN_REGION = 48
/** The waveform's share of the height when both displays are on; dragging the divider sets it. */
const split = useLocalStorage('vow.audioEditor.split', 0.5)

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
  const room = lanesHeight.value - SPLIT_GAP
  const waveHeight = Math.round(
    Math.max(Math.min(MIN_REGION, room / 2), Math.min(room - MIN_REGION, room * split.value)),
  )
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

const spectralSettings = computed(() => workspace.spectralSettings)
/** The frequency range the spectral display shows, top to bottom. */
const axis = computed(() =>
  resolveAxis(spectralSettings.value.scale, props.doc.sampleRate || 48000, workspace.frequencyView),
)

const spectrogram = useViewSpectrogram({
  key: () => props.doc.id,
  channels: () => shown.value.channels,
  sampleRate: () => props.doc.sampleRate,
  view: () => view.value,
  size: () => ({ width: size.value.width, height: regions.value.spectral?.height ?? 0 }),
  enabled: () => !!regions.value.spectral,
  display: () => ({
    fftSize: spectralSettings.value.fftSize,
    axis: axis.value,
    rangeDb: spectralSettings.value.rangeDb,
  }),
})

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

/** Scrolls so the frames are on screen, leaving the zoom alone when they already are. */
function reveal(start: number, end: number) {
  const visible = size.value.width * view.value.samplesPerPixel
  if (start >= view.value.start && end <= view.value.start + visible) return
  if (end - start > visible * 0.9) {
    const width = Math.max(1, size.value.width)
    setView(start - (end - start) * 0.05, ((end - start) * 1.1) / width)
  } else {
    setView((start + end) / 2 - visible / 2, view.value.samplesPerPixel)
  }
}

/** Zooms so the frames fill the view, with a little room either side. */
function zoomTo(start: number, end: number) {
  const width = Math.max(1, size.value.width)
  setView(start - (end - start) * 0.05, ((end - start) * 1.1) / width)
}

defineExpose({ zoomIn, zoomOut, zoomToFit, zoomToSelection, reveal, zoomTo })

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

const spectrogramImages = new WeakMap<Spectrogram, { colors: string; image: HTMLCanvasElement }>()

function spectrogramImage(spectrogram: Spectrogram) {
  const colors = spectralSettings.value.colors
  const cached = spectrogramImages.get(spectrogram)
  if (cached?.colors === colors) return cached.image
  const table = spectrumTable(colors)
  const image = cached?.image ?? document.createElement('canvas')
  image.width = spectrogram.columns
  image.height = spectrogram.rows
  const context = image.getContext('2d')!
  const pixels = context.createImageData(spectrogram.columns, spectrogram.rows)
  for (let i = 0; i < spectrogram.intensity.length; i++) {
    const value = spectrogram.intensity[i]! * 3
    pixels.data[i * 4] = table[value]!
    pixels.data[i * 4 + 1] = table[value + 1]!
    pixels.data[i * 4 + 2] = table[value + 2]!
    pixels.data[i * 4 + 3] = 255
  }
  context.putImageData(pixels, 0, 0)
  spectrogramImages.set(spectrogram, { colors, image })
  return image
}

const TICK_STEPS = [
  0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900,
  1800, 3600, 7200, 14400,
]

function tickDecimals(step: number) {
  return step < 0.01 ? 3 : step < 0.1 ? 2 : step < 1 ? 1 : 0
}

function drawRuler(context: CanvasRenderingContext2D, width: number) {
  const { sampleRate } = props.doc
  context.fillStyle = colors.ruler
  context.fillRect(0, 0, width, RULER)
  if (!sampleRate) return
  context.font = '10px ui-monospace, SFMono-Regular, Menlo, monospace'
  context.textBaseline = 'middle'
  const pixelsPerSecond = sampleRate / view.value.samplesPerPixel
  const last = frameAt(width) / sampleRate
  // The widest label on screen, plus a gap, has to fit between two ticks.
  const step =
    TICK_STEPS.find(
      (candidate) =>
        candidate * pixelsPerSecond >=
        context.measureText(formatTime(last, tickDecimals(candidate))).width + 16,
    ) ?? TICK_STEPS[TICK_STEPS.length - 1]!
  const decimals = tickDecimals(step)
  const first = Math.floor(view.value.start / sampleRate / step) * step
  for (let index = 0; first + index * step <= last + step; index++) {
    // Multiplied rather than summed, so 0.1 steps do not drift into 0.30000000000000004.
    const time = first + index * step
    const x = Math.round(xAt(time * sampleRate)) + 0.5
    if (x < -80 || x > width + 80) continue
    context.fillStyle = colors.rulerTick
    context.fillRect(x, RULER - 8, 1, 8)
    for (let minor = 1; minor < 5; minor++) {
      const minorX = Math.round(xAt((time + (step * minor) / 5) * sampleRate)) + 0.5
      context.fillRect(minorX, RULER - 4, 1, 4)
    }
    context.fillStyle = colors.rulerText
    context.fillText(formatTime(time, decimals), x + 3, RULER / 2 - 2)
  }
}

/** The selection, or while a range marker is dragged the range it covers now. */
function shownSelection() {
  const live = liveMarker.value
  if (live && live.length > 0 && live.id === props.doc.selectedMarkerId)
    return { start: live.start, end: live.start + live.length }
  return props.doc.selection
}

function drawWaveform(context: CanvasRenderingContext2D, width: number, region: Region) {
  const { doc } = props
  const { channels, peaks: pyramid } = shown.value
  const lanes = Math.max(1, doc.channelCount)
  const laneHeight = region.height / lanes
  const spp = view.value.samplesPerPixel
  const level = pyramid ? pickLevel(pyramid, spp) : null
  const selection = shownSelection()

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

/** The spectral selection, or while one is being drawn what it will be once the drag is done. */
function shownSpectral() {
  const live = liveSpectral.value
  return live ? live.selection : props.doc.spectralSelection
}

let overlayCache: {
  selection: SpectralSelection
  start: number
  samplesPerPixel: number
  axis: FrequencyAxis
  height: number
  width: number
  spans: Intervals[]
} | null = null

/**
 * The selection's extent down each pixel column, in px from the top of the spectral region.
 * Cached, since playback redraws every frame and a long brush stroke has thousands of points.
 */
function selectionSpans(selection: SpectralSelection, width: number, height: number) {
  const { start, samplesPerPixel } = view.value
  const cache = overlayCache
  if (
    cache &&
    cache.selection === selection &&
    cache.start === start &&
    cache.samplesPerPixel === samplesPerPixel &&
    cache.axis === axis.value &&
    cache.height === height &&
    cache.width === width
  )
    return cache.spans
  const spans: Intervals[] = []
  for (let x = 0; x < width; x++) {
    const column: Intervals = []
    for (const [low, high] of selectionIntervals(selection, frameAt(x + 0.5))) {
      const top = Math.max(0, axisPosition(axis.value, high) * height)
      const bottom = Math.min(height, axisPosition(axis.value, low) * height)
      if (bottom > top) column.push([top, bottom])
    }
    spans.push(column.sort((a, b) => a[0] - b[0]))
  }
  overlayCache = { selection, start, samplesPerPixel, axis: axis.value, height, width, spans }
  return spans
}

/** Tints the selected areas and outlines them, edges included where they change across columns. */
function drawSpectralSelection(
  context: CanvasRenderingContext2D,
  width: number,
  region: Region,
  selection: SpectralSelection,
) {
  const spans = selectionSpans(selection, width, region.height)
  context.fillStyle = colors.spectralFill
  for (let x = 0; x < width; x++)
    for (const [top, bottom] of spans[x]!) context.fillRect(x, region.top + top, 1, bottom - top)
  context.fillStyle = colors.spectralEdge
  for (let x = 0; x < width; x++) {
    const column = spans[x]!
    for (const [top, bottom] of column) {
      context.fillRect(x, region.top + top, 1, 1)
      context.fillRect(x, region.top + bottom - 1, 1, 1)
    }
    // Where this column covers what its neighbour does not, there is a left or right edge.
    const previous = spans[x - 1] ?? []
    for (const [top, bottom] of [
      ...subtractIntervals(column, previous),
      ...subtractIntervals(previous, column),
    ])
      context.fillRect(x, region.top + top, 1, bottom - top)
  }
  const last = spans[width - 1] ?? []
  for (const [top, bottom] of last) context.fillRect(width - 1, region.top + top, 1, bottom - top)
}

function drawSpectral(context: CanvasRenderingContext2D, width: number, region: Region) {
  const image = spectrogram.value
  context.fillStyle = spectrumBackground(spectralSettings.value.colors)
  context.fillRect(0, region.top, width, region.height)
  context.save()
  context.beginPath()
  context.rect(0, region.top, width, region.height)
  context.clip()
  if (image) {
    // Drawn where its frames and frequencies fall now, so while the next one builds after a zoom
    // or scroll it slides and scales along.
    const left = xAt(image.start)
    const right = xAt(image.start + image.columns * image.hop)
    let top = region.top
    let bottom = region.top + region.height
    if (image.axis.scale === axis.value.scale) {
      top = region.top + axisPosition(axis.value, image.axis.high) * region.height
      bottom = region.top + axisPosition(axis.value, image.axis.low) * region.height
    }
    context.imageSmoothingEnabled = true
    context.drawImage(
      spectrogramImage(image),
      0,
      0,
      image.columns,
      image.rows,
      left,
      top,
      right - left,
      bottom - top,
    )
  }

  const selection = shownSpectral()
  if (selection) drawSpectralSelection(context, width, region, selection)
  const outline = liveSpectral.value?.outline
  if (outline && outline.length > 1) {
    context.strokeStyle = colors.spectralEdge
    context.lineWidth = 1
    context.setLineDash([4, 3])
    context.beginPath()
    outline.forEach((point, index) =>
      index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y),
    )
    context.closePath()
    context.stroke()
    context.setLineDash([])
  }
  context.restore()

  context.font = '10px ui-monospace, SFMono-Regular, Menlo, monospace'
  context.textBaseline = 'middle'
  for (const { frequency, y } of axisLabels(axis.value, region.height)) {
    const label = formatFrequency(frequency)
    const at = region.top + y
    context.fillStyle = 'rgba(12, 18, 37, 0.65)'
    context.fillRect(0, at - 6, context.measureText(label).width + 8, 12)
    context.fillStyle = colors.frequencyText
    context.fillText(label, 4, at)
  }
}

function drawMarkers(context: CanvasRenderingContext2D, width: number) {
  context.fillStyle = colors.markerLane
  context.fillRect(0, markerTop, width, MARKER_LANE)
  context.fillStyle = colors.laneLine
  context.fillRect(0, lanesTop - 1, width, 1)
  // The marker being dragged is drawn where the pointer has it; nothing else hears about the
  // drag until it is dropped, so moving one costs a redraw and nothing more.
  const live = liveMarker.value
  const markers = live
    ? props.doc.markers.map((marker) => (marker.id === live.id ? live : marker))
    : props.doc.markers
  if (markers.length === 0) return
  const selectedId = props.doc.selectedMarkerId
  const firstFrame = frameAt(-2)
  const lastFrame = frameAt(width + 2)
  // Boundaries fade down the waveform as they crowd together, so a long take zoomed out shows
  // its audio rather than a wall of lines; the lane above still shows every marker.
  const onScreen = markers.filter(
    (marker) => marker.start + marker.length >= firstFrame && marker.start <= lastFrame,
  ).length
  const lineAlpha = Math.max(0.1, Math.min(0.9, (width / Math.max(1, onScreen) - 4) / 30))
  context.font = '10px ui-sans-serif, system-ui, sans-serif'
  context.textBaseline = 'middle'
  const drawOne = (marker: Marker) => {
    const end = marker.start + marker.length
    if (end < firstFrame || marker.start > lastFrame) return
    const selected = marker.id === selectedId
    const left = Math.round(xAt(marker.start))
    const right = Math.max(left + 1, Math.round(xAt(end)))
    const line = selected ? colors.markerSelected : colors.marker
    context.globalAlpha = selected ? 1 : lineAlpha
    context.fillStyle = line
    context.fillRect(left, lanesTop, 1, lanesHeight.value)
    if (marker.length > 0) context.fillRect(right - 1, lanesTop, 1, lanesHeight.value)
    context.globalAlpha = 1
    if (marker.length > 0) {
      context.fillStyle = selected ? colors.markerSelectedFill : colors.markerFill
      context.fillRect(left, markerTop + 2, right - left, MARKER_LANE - 4)
      context.fillStyle = line
      context.fillRect(left, markerTop + 2, 2, MARKER_LANE - 4)
      context.fillRect(right - 2, markerTop + 2, 2, MARKER_LANE - 4)
    } else {
      context.fillStyle = line
      context.beginPath()
      context.moveTo(left - 5, markerTop + 2)
      context.lineTo(left + 5, markerTop + 2)
      context.lineTo(left, markerTop + MARKER_LANE - 2)
      context.fill()
    }
    // A range that starts off screen keeps its name at the left edge, like a sticky header.
    const textLeft = marker.length > 0 ? Math.max(left, 0) + 5 : left + 7
    const room = marker.length > 0 ? Math.min(right, width) - textLeft - 3 : 120
    if (room > 10 && marker.name) {
      context.save()
      context.beginPath()
      context.rect(textLeft - 1, markerTop, room, MARKER_LANE)
      context.clip()
      context.fillStyle = selected ? '#ffffff' : colors.markerText
      context.fillText(marker.name, textLeft, markerTop + MARKER_LANE / 2)
      context.restore()
    }
  }
  for (const marker of markers) if (marker.id !== selectedId) drawOne(marker)
  const selected = markers.find((marker) => marker.id === selectedId)
  if (selected) drawOne(selected)
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
    // Markers as ticks along the top, so they can be found from anywhere in a long take.
    context.fillStyle = colors.marker
    for (const marker of doc.markers)
      context.fillRect(Math.floor((marker.start / doc.frames) * width), top, 1, 3)
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
    // The divider, with a grip in the middle to show it can be dragged.
    const top = waveform.top + waveform.height
    context.fillStyle = colors.ruler
    context.fillRect(0, top, width, SPLIT_GAP)
    context.fillStyle = colors.rulerTick
    const middle = Math.round(top + SPLIT_GAP / 2)
    context.fillRect(Math.round(width / 2 - 14), middle - 2, 28, 1)
    context.fillRect(Math.round(width / 2 - 14), middle + 1, 28, 1)
  }

  const selection = shownSelection()
  if (selection) {
    const left = Math.max(0, xAt(selection.start))
    const right = Math.min(width, xAt(selection.end))
    if (right > left) {
      context.fillStyle = colors.selection
      if (shownSpectral() && spectral) {
        // The outlined areas show what is picked on the spectrogram; the tint stays on the wave.
        if (waveform) context.fillRect(left, waveform.top, right - left, waveform.height)
      } else context.fillRect(left, lanesTop, right - left, lanesHeight.value)
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
  drawMarkers(context, width)

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
    props.doc.selection,
    props.doc.cursor,
    props.doc.view,
    props.doc.markers,
    liveMarker.value,
    props.doc.selectedMarkerId,
    props.doc.spectralSelection,
    liveSpectral.value,
    axis.value,
    spectralSettings.value.colors,
    props.playhead,
    props.views.waveform,
    props.views.spectral,
    props.cutPreview?.start,
    props.cutPreview?.end,
    shown.value,
    spectrogram.value,
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
  | { kind: 'split' }
  | {
      kind: 'spectral'
      tool: Exclude<SpectralTool, 'time'>
      /** Shift adds to the selection and Alt cuts out of it; the spot healing brush only heals. */
      combine: 'replace' | 'add' | 'subtract'
      base: SpectralSelection
      anchor: SpectralPoint
      downX: number
      downY: number
      moved: boolean
      shape: SpectralShape | null
      /** The lasso's points, on the audio and on screen. */
      points: SpectralPoint[]
      outline: { x: number; y: number }[]
    }
  | {
      kind: 'marker'
      id: number
      /** What is being dragged: one edge, or the whole marker. */
      part: 'start' | 'end' | 'body'
      grabFrame: number
      original: Marker
      before: Marker[]
      moved: boolean
    }

let drag: Drag | null = null
const hoverCursor = ref('text')
const hoverMarker = ref<Marker | null>(null)
/** The pointer over the spectral display, for the brush outline and the readout. */
const hoverSpectral = ref<{ x: number; y: number } | null>(null)

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

function inMarkerLane(y: number) {
  return y >= markerTop && y < lanesTop
}

/** Over the divider between the two displays, with a little slack either side to grab it. */
function inSplit(y: number) {
  const { waveform, spectral } = regions.value
  if (!waveform || !spectral) return false
  const top = waveform.top + waveform.height
  return y >= top - 2 && y < top + SPLIT_GAP + 2
}

function dragSplit(y: number) {
  const room = Math.max(1, lanesHeight.value - SPLIT_GAP)
  split.value = Math.min(0.95, Math.max(0.05, (y - lanesTop - SPLIT_GAP / 2) / room))
}

function inSpectral(y: number) {
  const region = regions.value.spectral
  return !!region && y >= region.top && y < region.top + region.height
}

/** The time and frequency under a point on the spectral display, clamped to the file. */
function spectralPoint(x: number, y: number): SpectralPoint {
  const region = regions.value.spectral!
  const position = Math.min(1, Math.max(0, (y - region.top) / region.height))
  return {
    frame: Math.max(0, Math.min(props.doc.frames, frameAt(x))),
    frequency: axisFrequency(axis.value, position),
  }
}

/** A brush stroke starting at `point`, round on screen at the brush size and the current zoom. */
function startStroke(point: SpectralPoint): Extract<SpectralShape, { kind: 'brush' }> {
  const radius = Math.max(1, spectralSettings.value.brushSize / 2)
  const region = regions.value.spectral!
  const { scale, low, high } = axis.value
  return extendStroke(
    {
      kind: 'brush',
      scale,
      points: [],
      radiusFrames: Math.max(0.5, radius * view.value.samplesPerPixel),
      radiusWarp: (radius / Math.max(1, region.height)) * (warp(high, scale) - warp(low, scale)),
    },
    point,
  )
}

/** The drag's shape joined to what was selected before, as its modifier keys asked. */
function combined(state: Extract<Drag, { kind: 'spectral' }>, shape: SpectralShape) {
  if (state.combine === 'replace' || state.tool === 'heal') return [{ shape, subtract: false }]
  return [...state.base, { shape, subtract: state.combine === 'subtract' }]
}

function updateSpectralDrag(state: Extract<Drag, { kind: 'spectral' }>, x: number, y: number) {
  const region = regions.value.spectral
  if (!region) return
  if (!state.moved && Math.hypot(x - state.downX, y - state.downY) < 3) return
  state.moved = true
  const clampedX = Math.min(size.value.width, Math.max(0, x))
  const clampedY = Math.min(region.top + region.height, Math.max(region.top, y))
  const point = spectralPoint(clampedX, clampedY)
  const { anchor } = state
  switch (state.tool) {
    case 'marquee':
    case 'frequency':
      state.shape = {
        kind: 'rect',
        start: state.tool === 'frequency' ? 0 : Math.min(anchor.frame, point.frame),
        end: state.tool === 'frequency' ? props.doc.frames : Math.max(anchor.frame, point.frame),
        low: Math.min(anchor.frequency, point.frequency),
        high: Math.max(anchor.frequency, point.frequency),
      }
      break
    case 'lasso': {
      const last = state.outline[state.outline.length - 1]!
      if (Math.hypot(clampedX - last.x, clampedY - last.y) < 2) return
      state.outline.push({ x: clampedX, y: clampedY })
      state.points.push(point)
      state.shape = { kind: 'lasso', scale: axis.value.scale, points: state.points.slice() }
      break
    }
    case 'brush':
    case 'heal':
      if (state.shape?.kind === 'brush') state.shape = extendStroke(state.shape, point)
      break
  }
  liveSpectral.value = {
    selection: state.shape ? combined(state, state.shape) : null,
    outline: state.tool === 'lasso' ? state.outline.slice() : null,
  }
}

/** Zooms (or with Shift scrolls) the frequency axis around the pointer. */
function wheelFrequency(event: WheelEvent, y: number) {
  const region = regions.value.spectral!
  const sampleRate = props.doc.sampleRate || 48000
  const delta = event.deltaY || event.deltaX
  const next = event.shiftKey
    ? panAxis(axis.value, sampleRate, -delta * 0.002)
    : zoomAxis(axis.value, sampleRate, (y - region.top) / region.height, Math.pow(1.0015, delta))
  const full = fullAxis(next.scale, sampleRate)
  workspace.frequencyView =
    next.low <= full.low * 1.0001 && next.high >= full.high * 0.9999
      ? null
      : { low: next.low, high: next.high }
}

/** The marker under `x` in the marker lane and the part of it there, the selected one first. */
function markerAt(x: number): { marker: Marker; part: 'start' | 'end' | 'body' } | null {
  const markers = props.doc.markers
  const selected = markers.find((marker) => marker.id === props.doc.selectedMarkerId)
  const ordered = selected ? [selected, ...markers.slice().reverse()] : markers.slice().reverse()
  for (const marker of ordered) {
    const left = xAt(marker.start)
    if (marker.length === 0) {
      if (Math.abs(x - left) <= EDGE_GRAB) return { marker, part: 'body' }
      continue
    }
    const right = xAt(marker.start + marker.length)
    // Narrow ranges are moved whole: there is no room to grab one edge.
    if (right - left > EDGE_GRAB * 3) {
      if (Math.abs(x - left) <= EDGE_GRAB) return { marker, part: 'start' }
      if (Math.abs(x - right) <= EDGE_GRAB) return { marker, part: 'end' }
    }
    if (x >= left - 2 && x <= right + 2) return { marker, part: 'body' }
  }
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
  if (inMarkerLane(y)) {
    const hit = markerAt(x)
    if (hit) {
      drag = {
        kind: 'marker',
        id: hit.marker.id,
        part: hit.part,
        grabFrame: frameAt(x),
        original: hit.marker,
        before: props.doc.markers,
        moved: false,
      }
      emit('markerSelect', hit.marker.id)
      return
    }
  }
  if (y < lanesTop) {
    drag = { kind: 'ruler' }
    emit('select', props.doc.selection, clampFrame(frameAt(x)))
    return
  }
  if (inSplit(y)) {
    drag = { kind: 'split' }
    return
  }
  const tool = spectralSettings.value.tool
  if (tool !== 'time' && inSpectral(y)) {
    const point = spectralPoint(x, y)
    drag = {
      kind: 'spectral',
      tool,
      combine: event.altKey ? 'subtract' : event.shiftKey ? 'add' : 'replace',
      base: props.doc.spectralSelection ?? [],
      anchor: point,
      downX: x,
      downY: y,
      moved: false,
      shape: null,
      points: [point],
      outline: [{ x, y }],
    }
    // A brush paints from the first press, so a click leaves a dab.
    if (tool === 'brush' || tool === 'heal') {
      drag.shape = startStroke(point)
      drag.moved = true
      liveSpectral.value = { selection: combined(drag, drag.shape), outline: null }
    }
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

/** The dragged marker after moving the pointer to `frame`. */
function draggedMarker(state: Extract<Drag, { kind: 'marker' }>, frame: number): Marker {
  const { original, part } = state
  const end = original.start + original.length
  if (part === 'body') {
    const start = Math.max(
      0,
      Math.min(
        props.doc.frames - original.length,
        Math.round(original.start + frame - state.grabFrame),
      ),
    )
    return { ...original, start }
  }
  const moved = clampFrame(frame)
  const [a, b] = part === 'start' ? [moved, end] : [original.start, moved]
  return { ...original, start: Math.min(a, b), length: Math.max(1, Math.abs(b - a)) }
}

function onPointerMove(event: PointerEvent) {
  const { x, y } = localPoint(event)
  hoverSpectral.value = inSpectral(y) ? { x, y } : null
  if (drag?.kind === 'spectral') {
    updateSpectralDrag(drag, x, y)
    return
  }
  if (!drag) {
    const hit = inMarkerLane(y) ? markerAt(x) : null
    hoverMarker.value = hit?.marker ?? null
    if (inSplit(y)) {
      hoverCursor.value = 'row-resize'
      return
    }
    const tool = spectralSettings.value.tool
    if (tool !== 'time' && inSpectral(y)) {
      hoverCursor.value = tool === 'brush' || tool === 'heal' ? 'none' : 'crosshair'
      return
    }
    hoverCursor.value =
      y >= size.value.height - OVERVIEW
        ? 'grab'
        : hit
          ? hit.part === 'body'
            ? 'grab'
            : 'ew-resize'
          : y < lanesTop
            ? 'pointer'
            : edgeAt(x) !== null
              ? 'col-resize'
              : 'text'
    return
  }
  const frame = clampFrame(frameAt(Math.max(0, Math.min(size.value.width, x))))
  if (drag.kind === 'split') dragSplit(y)
  else if (drag.kind === 'overview') panOverview(x)
  else if (drag.kind === 'ruler') emit('select', props.doc.selection, frame)
  else if (drag.kind === 'edge') updateSelection(drag.fixed, frame)
  else if (drag.kind === 'marker') {
    if (!drag.moved && Math.abs(frameAt(x) - drag.grabFrame) / view.value.samplesPerPixel < 3)
      return
    drag.moved = true
    liveMarker.value = draggedMarker(drag, frameAt(x))
  } else {
    if (!drag.moved && Math.abs(x - drag.downX) < 3) return
    drag.moved = true
    updateSelection(drag.anchor, frame)
  }
}

function onPointerUp(event: PointerEvent) {
  if (drag?.kind === 'spectral') {
    const state = drag
    const lassoTooSmall = state.tool === 'lasso' && state.points.length < 3
    if (!state.moved || !state.shape || lassoTooSmall) {
      // A click, like one on the waveform: drops the selection and puts the cursor there.
      if (state.combine === 'replace') {
        emit('spectralSelect', null)
        emit('select', null, clampFrame(frameAt(localPoint(event).x)))
      }
    } else if (state.tool === 'heal') emit('spotHeal', [{ shape: state.shape, subtract: false }])
    else emit('spectralSelect', combined(state, state.shape))
    liveSpectral.value = null
    drag = null
    return
  }
  if (drag?.kind === 'select' && !drag.moved && !drag.extend) {
    emit('select', null, clampFrame(frameAt(localPoint(event).x)))
  } else if (drag?.kind === 'marker' && drag.moved) {
    const state = drag
    const next = draggedMarker(state, frameAt(localPoint(event).x))
    emit(
      'markersCommit',
      state.part === 'body' ? 'Move marker' : 'Resize marker',
      state.before.map((marker) => (marker.id === state.id ? next : marker)),
      state.before,
    )
    // The selection follows the range it was showing.
    if (next.length > 0)
      emit('select', { start: next.start, end: next.start + next.length }, next.start)
  }
  liveMarker.value = null
  drag = null
}

/** What a right-click is on, for the marker menu: a marker in the lane, else a spot in time. */
const menuTarget = shallowRef<{ marker: Marker | null; frame: number | null }>({
  marker: null,
  frame: null,
})

function onContextMenu(event: MouseEvent) {
  const { x, y } = localPoint(event)
  const marker = inMarkerLane(y) ? (markerAt(x)?.marker ?? null) : null
  const onTimeline = y < size.value.height - OVERVIEW
  menuTarget.value = { marker, frame: onTimeline ? clampFrame(frameAt(x)) : null }
  // Highlight what the menu is about, without moving the selection.
  if (marker) workspace.highlightMarker(props.doc, marker.id)
}

function onDoubleClick(event: MouseEvent) {
  if (!props.doc.frames) return
  const { x, y } = localPoint(event)
  if (inMarkerLane(y)) return
  if (inSplit(y)) {
    split.value = 0.5
    return
  }
  if (inSpectral(y)) {
    // On the frequency labels: back to the full range.
    if (x < 44) {
      workspace.frequencyView = null
      return
    }
    if (spectralSettings.value.tool !== 'time') return
  }
  emit('select', { start: 0, end: props.doc.frames }, 0)
}

function onWheel(event: WheelEvent) {
  if (!props.doc.frames) return
  const { x, y } = localPoint(event)
  if (event.altKey && inSpectral(y)) {
    wheelFrequency(event, y)
    return
  }
  const horizontal = event.shiftKey ? event.deltaY : event.deltaX
  if (Math.abs(horizontal) > Math.abs(event.deltaY) || event.shiftKey) {
    setView(view.value.start + horizontal * view.value.samplesPerPixel, view.value.samplesPerPixel)
  } else {
    zoomAround(x, Math.pow(1.0015, event.deltaY))
  }
}

/** Time, frequency and level under the pointer on the spectral display. */
const spectralReadout = computed(() => {
  const hover = hoverSpectral.value
  const region = regions.value.spectral
  if (!hover || !region || !props.doc.sampleRate) return null
  const point = spectralPoint(hover.x, hover.y)
  const parts = [
    formatTime(point.frame / props.doc.sampleRate, 3),
    `${formatFrequency(point.frequency)} Hz`,
  ]
  const image = spectrogram.value
  const current = axis.value
  if (
    image &&
    image.axis.scale === current.scale &&
    image.axis.low === current.low &&
    image.axis.high === current.high
  ) {
    const column = Math.floor((point.frame - image.start) / image.hop)
    const row = Math.round(((hover.y - region.top) / region.height) * (image.rows - 1))
    if (column >= 0 && column < image.columns && row >= 0 && row < image.rows) {
      const level = intensityDb(image.intensity[row * image.columns + column]!, image.rangeDb)
      parts.push(level <= -11.5 - image.rangeDb ? 'silent' : `${Math.round(level)} dB`)
    }
  }
  return parts.join(' · ')
})

/** The outline of the brush, drawn where it will paint. */
const brushOutline = computed(() => {
  const hover = hoverSpectral.value
  const tool = spectralSettings.value.tool
  if (!hover || (tool !== 'brush' && tool !== 'heal')) return null
  const size = spectralSettings.value.brushSize
  return { left: hover.x - size / 2, top: hover.y - size / 2, size, heal: tool === 'heal' }
})

const actionBar = ref<HTMLDivElement | null>(null)
const { width: actionBarWidth, height: actionBarHeight } = useElementSize(actionBar)

/**
 * Where the spectral selection's actions float: centred over it, above it when there is room in
 * the spectrogram, else below it, else along the top. Hidden while drawing and when off screen.
 */
const actionBarPosition = computed(() => {
  const region = regions.value.spectral
  const bounds = selectionBounds(props.doc.spectralSelection)
  if (!region || !bounds || liveSpectral.value || props.doc.status !== 'ready') return null
  const width = size.value.width
  const left = xAt(bounds.start)
  const right = xAt(bounds.end)
  if (right < 0 || left > width) return null
  const top = region.top + Math.max(0, axisPosition(axis.value, bounds.high)) * region.height
  const bottom = region.top + Math.min(1, axisPosition(axis.value, bounds.low)) * region.height
  const barHeight = actionBarHeight.value || 36
  const gap = 8
  const y =
    top - barHeight - gap >= region.top + 2
      ? top - barHeight - gap
      : bottom + gap + barHeight <= region.top + region.height - 2
        ? bottom + gap
        : region.top + 6
  const half = (actionBarWidth.value || 400) / 2
  const centre = (Math.max(0, left) + Math.min(width, right)) / 2
  return { left: Math.min(width - half - 6, Math.max(half + 6, centre)), top: y }
})

const hoverTitle = computed(() => {
  const marker = hoverMarker.value
  if (!marker) return undefined
  const seconds = (frames: number) => formatTime(frames / (props.doc.sampleRate || 1))
  const span =
    marker.length > 0
      ? `${seconds(marker.start)} – ${seconds(marker.start + marker.length)}`
      : seconds(marker.start)
  return `${marker.name || 'Marker'} · ${span}${marker.comment ? `\n${marker.comment}` : ''}`
})
</script>

<template>
  <div
    ref="container"
    class="relative min-h-0 flex-1 overflow-hidden rounded-md border bg-[#120d1b]"
  >
    <MarkerMenu :doc="doc" :target="menuTarget">
      <canvas
        ref="canvas"
        class="absolute inset-0 block h-full w-full touch-none select-none"
        :style="{ cursor: hoverCursor }"
        role="img"
        :title="hoverTitle"
        :aria-label="`Waveform of ${doc.name}, ${formatTime(doc.frames / (doc.sampleRate || 1), 2)} long, ${doc.markers.length} marker(s)`"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
        @pointerleave="((hoverMarker = null), (hoverSpectral = null))"
        @dblclick="onDoubleClick"
        @wheel.prevent="onWheel"
        @contextmenu="onContextMenu"
      />
    </MarkerMenu>
    <div
      v-if="brushOutline"
      class="pointer-events-none absolute rounded-full border shadow-[0_0_0_1px_rgba(0,0,0,0.5)]"
      :class="brushOutline.heal ? 'border-[#8ee3ff] bg-[#8ee3ff]/10' : 'border-white/90'"
      :style="{
        left: `${brushOutline.left}px`,
        top: `${brushOutline.top}px`,
        width: `${brushOutline.size}px`,
        height: `${brushOutline.size}px`,
      }"
    />
    <div
      v-if="spectralReadout && regions.spectral"
      class="pointer-events-none absolute right-2 rounded bg-black/65 px-2 py-0.5 font-mono text-[11px] tabular-nums text-[#d6e2f5]"
      :style="{ top: `${regions.spectral.top + regions.spectral.height - 24}px` }"
    >
      {{ spectralReadout }}
    </div>
    <div
      v-if="regions.spectral && doc.status === 'ready'"
      class="absolute right-2"
      :style="{ top: `${regions.spectral.top + 6}px` }"
    >
      <SpectralSettings />
    </div>
    <!-- Hidden rather than unmounted off screen, so a preview keeps playing through a page turn. -->
    <div
      v-if="regions.spectral && doc.spectralSelection && doc.status === 'ready'"
      v-show="actionBarPosition"
      ref="actionBar"
      class="absolute z-[5] -translate-x-1/2"
      :style="{
        left: `${actionBarPosition?.left ?? 0}px`,
        top: `${actionBarPosition?.top ?? 0}px`,
      }"
    >
      <SpectralActionBar :doc="doc" />
    </div>
    <div
      v-if="previewLabel && doc.status === 'ready'"
      class="pointer-events-none absolute rounded bg-black/60 px-2 py-0.5 text-xs font-medium text-[#e6c8f7]"
      :class="regions.spectral?.top === lanesTop ? 'right-11' : 'right-2'"
      :style="{ top: `${lanesTop + 6}px` }"
    >
      {{ previewLabel }}
    </div>
    <div
      v-if="busy"
      class="pointer-events-none absolute left-1/2 z-10 flex -translate-x-1/2 items-center gap-2 rounded-full border border-white/10 bg-black/75 px-3 py-1 text-xs font-medium text-[#e6c8f7] shadow-lg"
      :style="{ top: `${lanesTop + 8}px` }"
      role="status"
      aria-live="polite"
    >
      <Loader2 class="size-3.5 animate-spin" />
      {{ busy }}
    </div>
    <div
      v-if="doc.status === 'loading'"
      class="absolute inset-0 grid place-items-center text-sm text-[#a99cba]"
    >
      <span class="flex items-center gap-2"><Loader2 class="size-4 animate-spin" /> Decoding…</span>
    </div>
    <div
      v-else-if="doc.status === 'error'"
      class="absolute inset-0 grid place-items-center px-6 text-center text-sm text-[#ff9db8]"
    >
      {{ doc.error ?? 'This file could not be opened.' }}
    </div>
    <div
      v-else-if="regions.spectral && !spectrogram"
      class="pointer-events-none absolute inset-x-0 grid place-items-center text-sm text-[#a99cba]"
      :style="{ top: `${regions.spectral.top}px`, height: `${regions.spectral.height}px` }"
    >
      <span class="flex items-center gap-2">
        <Loader2 class="size-4 animate-spin" /> Building spectrogram…
      </span>
    </div>
  </div>
</template>
