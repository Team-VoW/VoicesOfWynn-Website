// How frequencies are laid out on the spectral display: log (octaves equally tall, the default for
// voice), linear (harmonics equally spaced) or mel (in between, roughly how pitch is heard).

export type FrequencyScale = 'log' | 'linear' | 'mel'

/** The vertical range shown, in Hz, and how it is spread over the height. */
export interface FrequencyAxis {
  scale: FrequencyScale
  low: number
  high: number
}

/** The lowest frequency a scale can show; a log scale never reaches 0 Hz. */
export function scaleFloor(scale: FrequencyScale) {
  return scale === 'log' ? 20 : 0
}

/** The frequency in the scale's own units, in which equal steps are equally tall on screen. */
export function warp(frequency: number, scale: FrequencyScale) {
  switch (scale) {
    case 'log':
      return Math.log2(Math.max(1, frequency))
    case 'mel':
      return 2595 * Math.log10(1 + Math.max(0, frequency) / 700)
    case 'linear':
      return frequency
  }
}

export function unwarp(value: number, scale: FrequencyScale) {
  switch (scale) {
    case 'log':
      return Math.pow(2, value)
    case 'mel':
      return 700 * (Math.pow(10, value / 2595) - 1)
    case 'linear':
      return value
  }
}

/** The whole range a file at `sampleRate` can show on `scale`. */
export function fullAxis(scale: FrequencyScale, sampleRate: number): FrequencyAxis {
  return { scale, low: scaleFloor(scale), high: Math.max(scaleFloor(scale) + 1, sampleRate / 2) }
}

/** `view` (a zoomed range in Hz, or null for everything) fitted to the scale and the file's rate. */
export function resolveAxis(
  scale: FrequencyScale,
  sampleRate: number,
  view: { low: number; high: number } | null,
): FrequencyAxis {
  const full = fullAxis(scale, sampleRate)
  if (!view) return full
  const low = Math.max(full.low, view.low)
  const high = Math.min(full.high, view.high)
  return high > low ? { scale, low, high } : full
}

/** Where `frequency` sits on the axis: 0 at the top (high), 1 at the bottom (low). */
export function axisPosition(axis: FrequencyAxis, frequency: number) {
  const top = warp(axis.high, axis.scale)
  const bottom = warp(axis.low, axis.scale)
  return (top - warp(frequency, axis.scale)) / (top - bottom)
}

/** The frequency at `position` (0 top, 1 bottom) on the axis. */
export function axisFrequency(axis: FrequencyAxis, position: number) {
  const top = warp(axis.high, axis.scale)
  const bottom = warp(axis.low, axis.scale)
  return unwarp(top - position * (top - bottom), axis.scale)
}

/**
 * Zooms the axis by `factor` (below 1 zooms in) around the frequency at `position`, which stays
 * where it is on screen. Never past the full range, nor closer than a few semitones.
 */
export function zoomAxis(
  axis: FrequencyAxis,
  sampleRate: number,
  position: number,
  factor: number,
): FrequencyAxis {
  const { scale } = axis
  const full = fullAxis(scale, sampleRate)
  const fullTop = warp(full.high, scale)
  const fullBottom = warp(full.low, scale)
  const top = warp(axis.high, scale)
  const bottom = warp(axis.low, scale)
  const anchor = top - position * (top - bottom)
  const minimum = Math.max(
    (fullTop - fullBottom) / 400,
    warp(axisFrequency(axis, position) * 1.25, scale) - warp(axisFrequency(axis, position), scale),
  )
  const span = Math.min(fullTop - fullBottom, Math.max(minimum, (top - bottom) * factor))
  let nextTop = anchor + position * span
  let nextBottom = nextTop - span
  if (nextTop > fullTop) [nextTop, nextBottom] = [fullTop, fullTop - span]
  if (nextBottom < fullBottom) [nextTop, nextBottom] = [fullBottom + span, fullBottom]
  return { scale, low: unwarp(nextBottom, scale), high: unwarp(nextTop, scale) }
}

/** Moves the range up (positive) or down by `fraction` of its height, staying inside the full range. */
export function panAxis(axis: FrequencyAxis, sampleRate: number, fraction: number): FrequencyAxis {
  const { scale } = axis
  const full = fullAxis(scale, sampleRate)
  const top = warp(axis.high, scale)
  const bottom = warp(axis.low, scale)
  const span = top - bottom
  const shift = Math.max(
    warp(full.low, scale) - bottom,
    Math.min(warp(full.high, scale) - top, fraction * span),
  )
  return { scale, low: unwarp(bottom + shift, scale), high: unwarp(top + shift, scale) }
}

const LABEL_PRIORITY = [1, 5, 2, 3, 4, 6, 7, 8, 9, 1.5, 2.5]

/**
 * Frequencies worth labelling on an axis `height` px tall, at least `gap` px apart: round ones
 * first (1k, 10k), then the in-betweens as the zoom leaves room for them.
 */
export function axisLabels(axis: FrequencyAxis, height: number, gap = 18) {
  const placed: { frequency: number; y: number }[] = []
  const lowest = Math.max(1, axis.low)
  for (const mantissa of LABEL_PRIORITY) {
    for (
      let decade = Math.pow(10, Math.floor(Math.log10(lowest)));
      decade <= axis.high;
      decade *= 10
    ) {
      const frequency = mantissa * decade
      if (frequency < axis.low || frequency > axis.high) continue
      const y = axisPosition(axis, frequency) * height
      if (y < gap / 2 || y > height - gap / 3) continue
      if (placed.some((label) => Math.abs(label.y - y) < gap)) continue
      placed.push({ frequency, y })
    }
  }
  return placed.sort((a, b) => a.y - b.y)
}

export function formatFrequency(frequency: number) {
  if (frequency >= 1000) {
    const k = frequency / 1000
    return `${Number(k.toFixed(k >= 10 ? 1 : 2))}k`
  }
  return `${Math.round(frequency)}`
}
