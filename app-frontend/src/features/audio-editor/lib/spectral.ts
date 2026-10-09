// Spectral editing, as in Audition's Spectral Frequency Display and RX's Spectral Repair: pick out
// areas of time × frequency with a marquee, lasso or brush, then turn them up or down, delete
// them, heal them from the sound around them, or listen to them on their own. Edits go through a
// short-time Fourier transform whose untouched frames add back up to the input exactly, so audio
// outside the selection (in time or in frequency) comes out as it went in.

import { fft } from '@/components/audio/fft'
import { warp, unwarp, type FrequencyScale } from './frequencyScale'
import { dbToGain, frameCount, type Channels } from './operations'

/** A point on the spectral display, in frames and Hz. */
export interface SpectralPoint {
  frame: number
  frequency: number
}

export type SpectralShape =
  | { kind: 'rect'; start: number; end: number; low: number; high: number }
  /** A closed outline, straight between points on the scale it was drawn on. */
  | { kind: 'lasso'; scale: FrequencyScale; points: SpectralPoint[] }
  /**
   * Strokes of a round brush: ellipses around each point, `radiusFrames` wide and `radiusWarp` tall
   * in the units of the scale it was painted on, so it is round on screen as it was painted.
   */
  | {
      kind: 'brush'
      scale: FrequencyScale
      points: SpectralPoint[]
      radiusFrames: number
      radiusWarp: number
    }

/** Shapes in drawing order: each adds to what came before, or with `subtract` cuts out of it. */
export type SpectralSelection = { shape: SpectralShape; subtract: boolean }[]

export type SpectralMode = 'gain' | 'delete' | 'heal' | 'isolate'
export type HealDirection = 'auto' | 'time' | 'frequency'

export interface SpectralEdit {
  selection: SpectralSelection
  mode: SpectralMode
  /** For `gain`: how far the selection goes up or down. */
  gainDb: number
  /** 0 to 1: how far beyond the selection the edit fades out, so it never leaves a hard edge. */
  feather: number
  /** For `heal`: which neighbours to rebuild from. Auto takes the nearer ones. */
  direction: HealDirection
  /** Window length: longer resolves pitch finer, shorter resolves time finer. */
  fftSize: number
}

/** Half-open frequency ranges in Hz, sorted and apart. */
export type Intervals = [low: number, high: number][]

/** Bins and frames the widest feather reaches beyond the selection. */
export const MAX_FEATHER_CELLS = 6
/** Frames and bins either side of a healed area its replacement level is measured from. */
const HEAL_CONTEXT_FRAMES = 4
const HEAL_CONTEXT_BINS = 3
/** Healing only ever turns a bin down, and leaves this much headroom over its neighbours' level. */
const HEAL_HEADROOM = 1.2
/** Healing keeps the whole area's spectrum in memory, so it works on this much at a time. */
export const MAX_HEAL_SECONDS = 30
const OVERLAP_GAIN = 1.5

function mergeIntervals(intervals: Intervals): Intervals {
  const sorted = intervals.filter(([low, high]) => high > low).sort((a, b) => a[0] - b[0])
  const merged: Intervals = []
  for (const [low, high] of sorted) {
    const last = merged[merged.length - 1]
    if (last && low <= last[1]) last[1] = Math.max(last[1], high)
    else merged.push([low, high])
  }
  return merged
}

export function subtractIntervals(from: Intervals, cut: Intervals): Intervals {
  let result = from.map(([low, high]) => [low, high] as [number, number])
  for (const [cutLow, cutHigh] of cut) {
    const next: Intervals = []
    for (const [low, high] of result) {
      if (cutHigh <= low || cutLow >= high) next.push([low, high])
      else {
        if (cutLow > low) next.push([low, cutLow])
        if (cutHigh < high) next.push([cutHigh, high])
      }
    }
    result = next
  }
  return result
}

function lassoIntervals(shape: Extract<SpectralShape, { kind: 'lasso' }>, frame: number) {
  const { points, scale } = shape
  // Where the vertical line at `frame` crosses the outline; inside is between pairs of crossings.
  const crossings: number[] = []
  for (let i = 0; i < points.length; i++) {
    const a = points[i]!
    const b = points[(i + 1) % points.length]!
    if (a.frame <= frame !== b.frame <= frame) {
      const t = (frame - a.frame) / (b.frame - a.frame)
      const wa = warp(a.frequency, scale)
      crossings.push(wa + t * (warp(b.frequency, scale) - wa))
    }
  }
  crossings.sort((a, b) => a - b)
  const intervals: Intervals = []
  for (let i = 0; i + 1 < crossings.length; i += 2)
    intervals.push([unwarp(crossings[i]!, scale), unwarp(crossings[i + 1]!, scale)])
  return intervals
}

function brushIntervals(shape: Extract<SpectralShape, { kind: 'brush' }>, frame: number) {
  const { points, scale, radiusFrames, radiusWarp } = shape
  const intervals: Intervals = []
  for (const point of points) {
    const dx = (frame - point.frame) / radiusFrames
    if (dx <= -1 || dx >= 1) continue
    const half = radiusWarp * Math.sqrt(1 - dx * dx)
    const centre = warp(point.frequency, scale)
    intervals.push([unwarp(centre - half, scale), unwarp(centre + half, scale)])
  }
  return mergeIntervals(intervals)
}

/** The frequencies the shape covers at `frame`. */
export function shapeIntervals(shape: SpectralShape, frame: number): Intervals {
  switch (shape.kind) {
    case 'rect':
      return frame >= shape.start && frame < shape.end ? [[shape.low, shape.high]] : []
    case 'lasso':
      return lassoIntervals(shape, frame)
    case 'brush':
      return brushIntervals(shape, frame)
  }
}

/** The frequencies the selection covers at `frame`, its shapes added and cut out in order. */
export function selectionIntervals(selection: SpectralSelection, frame: number): Intervals {
  let result: Intervals = []
  for (const { shape, subtract } of selection) {
    const intervals = shapeIntervals(shape, frame)
    if (intervals.length === 0) continue
    result = subtract
      ? subtractIntervals(result, intervals)
      : mergeIntervals([...result, ...intervals])
  }
  return result
}

/** The frames and frequencies the shape spans. */
export function shapeBounds(shape: SpectralShape) {
  switch (shape.kind) {
    case 'rect':
      return { start: shape.start, end: shape.end, low: shape.low, high: shape.high }
    case 'lasso':
    case 'brush': {
      const reach = shape.kind === 'brush' ? shape.radiusFrames : 0
      const warpReach = shape.kind === 'brush' ? shape.radiusWarp : 0
      let start = Infinity,
        end = -Infinity,
        low = Infinity,
        high = -Infinity
      for (const point of shape.points) {
        const centre = warp(point.frequency, shape.scale)
        start = Math.min(start, point.frame - reach)
        end = Math.max(end, point.frame + reach)
        low = Math.min(low, unwarp(centre - warpReach, shape.scale))
        high = Math.max(high, unwarp(centre + warpReach, shape.scale))
      }
      return { start, end, low: Math.max(0, low), high }
    }
  }
}

/** The frames and frequencies the added shapes span, or null when nothing is added. */
export function selectionBounds(selection: SpectralSelection | null) {
  let bounds: ReturnType<typeof shapeBounds> | null = null
  for (const { shape, subtract } of selection ?? []) {
    if (subtract) continue
    const next = shapeBounds(shape)
    if (!Number.isFinite(next.start)) continue
    bounds = bounds
      ? {
          start: Math.min(bounds.start, next.start),
          end: Math.max(bounds.end, next.end),
          low: Math.min(bounds.low, next.low),
          high: Math.max(bounds.high, next.high),
        }
      : next
  }
  return bounds
}

/** The selection moved `frames` later (earlier when negative). */
export function offsetSelection(selection: SpectralSelection, frames: number): SpectralSelection {
  return selection.map(({ shape, subtract }) => ({
    subtract,
    shape:
      shape.kind === 'rect'
        ? { ...shape, start: shape.start + frames, end: shape.end + frames }
        : {
            ...shape,
            points: shape.points.map((point) => ({ ...point, frame: point.frame + frames })),
          },
  }))
}

/** How many frames either side of the selection an edit reads, for slicing the audio it needs. */
export function spectralReach(edit: SpectralEdit) {
  const hop = edit.fftSize / 4
  return edit.fftSize * 2 + (MAX_FEATHER_CELLS + HEAL_CONTEXT_FRAMES + 2) * hop
}

function featherCells(edit: SpectralEdit) {
  return Math.round(Math.max(0, Math.min(1, edit.feather)) * MAX_FEATHER_CELLS)
}

/** Triangle weights over -radius..radius summing to 1, the shape of two box blurs in a row. */
function triangle(radius: number) {
  const weights = Float32Array.from(
    { length: radius * 2 + 1 },
    (_, i) => radius + 1 - Math.abs(i - radius),
  )
  const total = weights.reduce((sum, weight) => sum + weight, 0)
  return weights.map((weight) => weight / total)
}

/**
 * The selection rasterised onto an STFT grid one column at a time, in order: 1 inside, falling to 0
 * over the feather outside. Each column covers its whole hop, so a selection narrower than a hop
 * (one click, zoomed in) still lands on the column it falls in.
 */
class MaskColumns {
  private readonly bins: number
  private readonly radius: number
  private readonly kernel: Float32Array
  /** Raw and frequency-blurred columns around the one asked for, keyed by column. */
  private readonly ring = new Map<number, { raw: Float32Array; soft: Float32Array }>()
  private readonly output: Float32Array
  /** First and last frame of every shape. */
  private readonly edges: number[]

  constructor(
    private readonly selection: SpectralSelection,
    private readonly binHz: number,
    fftSize: number,
    private readonly centre: (column: number) => number,
    private readonly hop: number,
    radius: number,
  ) {
    this.bins = fftSize / 2 + 1
    this.radius = radius
    this.kernel = triangle(radius)
    this.output = new Float32Array(this.bins)
    this.edges = selection.flatMap(({ shape }) => {
      const bounds = shapeBounds(shape)
      return [bounds.start, bounds.end - 0.5]
    })
  }

  private rawColumn(column: number) {
    const raw = new Float32Array(this.bins)
    const middle = this.centre(column)
    const from = middle - this.hop / 2
    const to = middle + this.hop / 2
    // A few points across the hop, plus any shape edge inside it, so nothing narrower slips between.
    const samples = [from + 0.5, middle - this.hop / 4, middle, middle + this.hop / 4, to - 0.5]
    for (const edge of this.edges) if (edge > from && edge < to) samples.push(edge)
    const intervals = mergeIntervals(
      samples.flatMap((frame) => selectionIntervals(this.selection, frame)),
    )
    for (const [low, high] of intervals) {
      // Every bin whose band overlaps the range: a thin brush stroke still catches its bin.
      const first = Math.max(0, Math.floor(low / this.binHz + 0.5))
      const last = Math.min(this.bins - 1, Math.ceil(high / this.binHz - 0.5))
      for (let bin = first; bin <= last; bin++) raw[bin] = 1
    }
    if (this.radius === 0) return { raw, soft: raw }
    const soft = new Float32Array(this.bins)
    const { radius, kernel } = this
    for (let bin = 0; bin < this.bins; bin++) {
      if (raw[bin] === 0) continue
      for (let d = -radius; d <= radius; d++) {
        const target = bin + d
        if (target >= 0 && target < this.bins) soft[target]! += kernel[d + radius]!
      }
    }
    return { raw, soft }
  }

  private entry(column: number) {
    let entry = this.ring.get(column)
    if (!entry) {
      entry = this.rawColumn(column)
      this.ring.set(column, entry)
      // Only the window around the column being read is ever needed again.
      this.ring.delete(column - this.radius * 2 - 2)
    }
    return entry
  }

  /** The mask for `column`, valid until the next call. Columns must be asked for in order. */
  column(column: number) {
    const { output, radius, kernel } = this
    if (radius === 0) {
      output.set(this.entry(column).raw)
      return output
    }
    output.fill(0)
    for (let d = -radius; d <= radius; d++) {
      const { soft } = this.entry(column + d)
      const weight = kernel[d + radius]!
      for (let bin = 0; bin < this.bins; bin++) output[bin]! += soft[bin]! * weight
    }
    const { raw } = this.entry(column)
    for (let bin = 0; bin < this.bins; bin++)
      output[bin] = Math.max(raw[bin]!, Math.min(1, output[bin]!))
    return output
  }
}

function hann(size: number) {
  return Float32Array.from(
    { length: size },
    (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / size),
  )
}

interface Grid {
  /** First frame of the stretch the STFT runs over, and one past its last. */
  from: number
  to: number
  /** The part of it the mask can reach; the rest is only read, so it stays bit for bit. */
  writeFrom: number
  writeTo: number
  fftSize: number
  hop: number
  bins: number
  columns: number
  binHz: number
  /** The frame at the middle of each column's window. */
  centre: (column: number) => number
}

/** The STFT grid over the selection and enough either side for the feather and heal context. */
function gridFor(
  frames: number,
  sampleRate: number,
  edit: SpectralEdit,
  bounds: { start: number; end: number },
): Grid {
  const fftSize = edit.fftSize
  const hop = fftSize / 4
  const reach = spectralReach(edit)
  const from = Math.max(0, Math.floor(bounds.start) - reach)
  const to = Math.min(frames, Math.ceil(bounds.end) + reach)
  // Windows start a whole window before `from`, so every frame in the stretch has full overlap.
  const columns = Math.max(0, Math.floor((to - from + fftSize) / hop) + 1)
  const touched = (featherCells(edit) + 2) * hop + fftSize
  return {
    from,
    to,
    writeFrom: Math.max(from, Math.floor(bounds.start) - touched),
    writeTo: Math.min(to, Math.ceil(bounds.end) + touched),
    fftSize,
    hop,
    bins: fftSize / 2 + 1,
    columns,
    binHz: sampleRate / fftSize,
    centre: (column) => from - fftSize + column * hop + fftSize / 2,
  }
}

/**
 * What a column's spectrum callback did: left it alone (it goes back exactly as it came, with no
 * transform either way), changed it, or silenced it.
 */
type ColumnResult = 'kept' | 'changed' | 'silent'

/** Runs `shape` over each column's spectrum and adds the result back up into the output. */
function resynthesize(
  channel: Float32Array,
  output: Float32Array,
  grid: Grid,
  shape: (column: number, real: Float32Array, imaginary: Float32Array) => ColumnResult,
) {
  const { from, to, fftSize, hop, columns } = grid
  const window = hann(fftSize)
  const real = new Float32Array(fftSize)
  const imaginary = new Float32Array(fftSize)
  const accumulated = new Float32Array(to - from + fftSize * 2)
  for (let column = 0; column < columns; column++) {
    const position = from - fftSize + column * hop
    for (let i = 0; i < fftSize; i++) {
      const frame = position + i
      real[i] = frame >= 0 && frame < channel.length ? channel[frame]! * window[i]! : 0
    }
    const offset = column * hop
    imaginary.fill(0)
    const result = shape(column, real, imaginary)
    if (result === 'changed') {
      for (let i = 0; i < fftSize; i++) imaginary[i] = -imaginary[i]!
      fft(real, imaginary)
      for (let i = 0; i < fftSize; i++)
        accumulated[offset + i]! += (real[i]! / fftSize) * window[i]!
    } else if (result === 'kept') {
      for (let i = 0; i < fftSize; i++) accumulated[offset + i]! += real[i]! * window[i]!
    }
  }
  for (let frame = grid.writeFrom; frame < grid.writeTo; frame++)
    output[frame] = accumulated[frame - from + fftSize]! / OVERLAP_GAIN
}

/** Scales bin `bin` (and its mirror image) of a full complex spectrum. */
function scaleBin(real: Float32Array, imaginary: Float32Array, bin: number, factor: number) {
  const size = real.length
  real[bin]! *= factor
  imaginary[bin]! *= factor
  if (bin > 0 && bin < size / 2) {
    real[size - bin]! *= factor
    imaginary[size - bin]! *= factor
  }
}

/** Gain, delete and isolate: each bin scaled by how far inside the selection it is. */
function scaleChannel(channel: Float32Array, output: Float32Array, grid: Grid, edit: SpectralEdit) {
  const mask = new MaskColumns(
    edit.selection,
    grid.binHz,
    grid.fftSize,
    grid.centre,
    grid.hop,
    featherCells(edit),
  )
  const target = edit.mode === 'gain' ? dbToGain(edit.gainDb) : 0
  const isolate = edit.mode === 'isolate'
  resynthesize(channel, output, grid, (column, real, imaginary) => {
    const weights = mask.column(column)
    if (!weights.some((weight) => weight > 0)) return isolate ? 'silent' : 'kept'
    fft(real, imaginary)
    for (let bin = 0; bin < grid.bins; bin++) {
      const weight = weights[bin]!
      const factor = isolate ? weight : 1 + (target - 1) * weight
      if (factor !== 1) scaleBin(real, imaginary, bin, factor)
    }
    return 'changed'
  })
}

/**
 * Average magnitude over the unselected cells next to a run of selected ones, walking away from
 * the run until `count` are found or another selected cell or the edge is reached.
 */
function contextLevel(
  magnitude: Float32Array,
  mask: Float32Array,
  start: number,
  step: number,
  limit: number,
  stride: number,
  base: number,
  count: number,
) {
  let sum = 0
  let found = 0
  for (let index = start; index >= 0 && index < limit && found < count; index += step) {
    const cell = base + index * stride
    if (mask[cell]! > 0) break
    sum += magnitude[cell]!
    found++
  }
  return found > 0 ? sum / found : null
}

/** Log-interpolates between the levels either side of a run, from 0 at its start to 1 past its end. */
function bridge(before: number | null, after: number | null, t: number) {
  if (before === null) return after
  if (after === null) return before
  return Math.exp(Math.log(before + 1e-12) * (1 - t) + Math.log(after + 1e-12) * t)
}

/**
 * Heal: each selected bin is brought down to the level of the audio around it, interpolated across
 * the gap in time (for clicks, coughs, plosives: short and tall) or in frequency (for whistles,
 * tones, squeaks: long and thin). Auto weighs the two by how close the nearest edge is each way.
 * Phase is kept, and nothing is ever turned up.
 */
function healChannel(channel: Float32Array, output: Float32Array, grid: Grid, edit: SpectralEdit) {
  const { columns, bins, fftSize } = grid
  const maskSource = new MaskColumns(
    edit.selection,
    grid.binHz,
    fftSize,
    grid.centre,
    grid.hop,
    featherCells(edit),
  )
  const mask = new Float32Array(columns * bins)
  const magnitude = new Float32Array(columns * bins)
  const window = hann(fftSize)
  const real = new Float32Array(fftSize)
  const imaginary = new Float32Array(fftSize)
  for (let column = 0; column < columns; column++) {
    mask.set(maskSource.column(column), column * bins)
    const position = grid.from - fftSize + column * grid.hop
    for (let i = 0; i < fftSize; i++) {
      const frame = position + i
      real[i] = frame >= 0 && frame < channel.length ? channel[frame]! * window[i]! : 0
    }
    imaginary.fill(0)
    fft(real, imaginary)
    for (let bin = 0; bin < bins; bin++)
      magnitude[column * bins + bin] = Math.hypot(real[bin]!, imaginary[bin]!)
  }

  // Levels interpolated along time, per bin, and along frequency, per column, with the length of
  // the run each cell is in: the shorter run has its edges closer and predicts the cell better.
  const timeLevel = new Float32Array(columns * bins).fill(NaN)
  const timeRun = new Float32Array(columns * bins)
  const frequencyLevel = new Float32Array(columns * bins).fill(NaN)
  const frequencyRun = new Float32Array(columns * bins)
  if (edit.direction !== 'frequency')
    for (let bin = 0; bin < bins; bin++) {
      for (let column = 0; column < columns; column++) {
        if (mask[column * bins + bin]! === 0) continue
        let end = column
        while (end < columns && mask[end * bins + bin]! > 0) end++
        const before = contextLevel(
          magnitude,
          mask,
          column - 1,
          -1,
          columns,
          bins,
          bin,
          HEAL_CONTEXT_FRAMES,
        )
        const after = contextLevel(magnitude, mask, end, 1, columns, bins, bin, HEAL_CONTEXT_FRAMES)
        for (let cell = column; cell < end; cell++) {
          const level = bridge(before, after, (cell - column + 1) / (end - column + 1))
          if (level === null) continue
          timeLevel[cell * bins + bin] = level
          timeRun[cell * bins + bin] = end - column
        }
        column = end
      }
    }
  if (edit.direction !== 'time')
    for (let column = 0; column < columns; column++) {
      const base = column * bins
      for (let bin = 0; bin < bins; bin++) {
        if (mask[base + bin]! === 0) continue
        let end = bin
        while (end < bins && mask[base + end]! > 0) end++
        const below = contextLevel(magnitude, mask, bin - 1, -1, bins, 1, base, HEAL_CONTEXT_BINS)
        const above = contextLevel(magnitude, mask, end, 1, bins, 1, base, HEAL_CONTEXT_BINS)
        for (let cell = bin; cell < end; cell++) {
          const level = bridge(below, above, (cell - bin + 1) / (end - bin + 1))
          if (level === null) continue
          frequencyLevel[base + cell] = level
          frequencyRun[base + cell] = end - bin
        }
        bin = end
      }
    }

  resynthesize(channel, output, grid, (column, real, imaginary) => {
    const base = column * bins
    let any = false
    for (let bin = 0; bin < bins && !any; bin++) any = mask[base + bin]! > 0
    if (!any) return 'kept'
    fft(real, imaginary)
    for (let bin = 0; bin < bins; bin++) {
      const cell = base + bin
      const weight = mask[cell]!
      if (weight === 0) continue
      const byTime = timeLevel[cell]!
      const byFrequency = frequencyLevel[cell]!
      let level: number
      if (Number.isNaN(byTime) && Number.isNaN(byFrequency)) continue
      else if (Number.isNaN(byFrequency)) level = byTime
      else if (Number.isNaN(byTime)) level = byFrequency
      else {
        const timeWeight = 1 / timeRun[cell]!
        const frequencyWeight = 1 / frequencyRun[cell]!
        level = Math.exp(
          (Math.log(byTime + 1e-12) * timeWeight +
            Math.log(byFrequency + 1e-12) * frequencyWeight) /
            (timeWeight + frequencyWeight),
        )
      }
      const current = magnitude[cell]!
      const healed = Math.min(1, (level * HEAL_HEADROOM) / (current + 1e-12))
      const factor = 1 - weight + weight * healed
      if (factor < 1) scaleBin(real, imaginary, bin, factor)
    }
    return 'changed'
  })
}

/** Why the edit cannot run, or null when it can. */
export function spectralProblem(edit: SpectralEdit, sampleRate: number) {
  const bounds = selectionBounds(edit.selection)
  if (!bounds) return 'Select an area of the spectrogram first.'
  if (edit.mode === 'heal' && bounds.end - bounds.start > MAX_HEAL_SECONDS * sampleRate)
    return `Heal works on up to ${MAX_HEAL_SECONDS} s at a time; select a shorter area.`
  return null
}

/** Runs a spectral edit over every channel. Isolate leaves silence everywhere but the selection. */
export function spectralEdit(channels: Channels, sampleRate: number, edit: SpectralEdit): Channels {
  const frames = frameCount(channels)
  const bounds = selectionBounds(edit.selection)
  const problem = spectralProblem(edit, sampleRate)
  if (problem && bounds) throw new Error(problem)
  return channels.map((channel) => {
    const output = edit.mode === 'isolate' ? new Float32Array(frames) : channel.slice()
    if (!bounds) return output
    const grid = gridFor(frames, sampleRate, edit, bounds)
    if (grid.to <= grid.from) return output
    if (edit.mode === 'heal') healChannel(channel, output, grid, edit)
    else scaleChannel(channel, output, grid, edit)
    return output
  })
}

/**
 * Adds `point` to a brush stroke, filling in between it and the last one so a fast drag leaves no
 * gaps. Spacing is measured on the brush's own ellipse, so it is the same on any scale or zoom.
 */
export function extendStroke(
  shape: Extract<SpectralShape, { kind: 'brush' }>,
  point: SpectralPoint,
): Extract<SpectralShape, { kind: 'brush' }> {
  const last = shape.points[shape.points.length - 1]
  if (!last) return { ...shape, points: [point] }
  const dx = (point.frame - last.frame) / shape.radiusFrames
  const lastWarp = warp(last.frequency, shape.scale)
  const dy = (warp(point.frequency, shape.scale) - lastWarp) / shape.radiusWarp
  const distance = Math.hypot(dx, dy)
  if (distance < 0.25) return shape
  const steps = Math.ceil(distance / 0.4)
  const added: SpectralPoint[] = []
  for (let step = 1; step <= steps; step++) {
    const t = step / steps
    added.push({
      frame: last.frame + (point.frame - last.frame) * t,
      frequency: unwarp(lastWarp + dy * shape.radiusWarp * t, shape.scale),
    })
  }
  return { ...shape, points: [...shape.points, ...added] }
}
