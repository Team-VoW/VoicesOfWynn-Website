// Automatic click removal for mouth clicks, lip smacks and digital ticks: find samples that jump
// far more than the audio around them, then rebuild each short gap from the waveform on both
// sides with least-squares autoregressive (LSAR) interpolation.

import type { Channels, FrameRange } from './operations'

export interface ClickOptions {
  /** How many times louder than its surroundings a jump must be; lower finds more. */
  threshold: number
  /** Anything longer is a consonant or a knock, not a click, and is left alone. */
  maxClickMs: number
}

const BLOCK = 256
/** Blocks on each side whose median sets the local level, so a click cannot raise its own bar. */
const NEIGHBOURS = 4
/** Jumps closer than this (0.5 ms at 48 kHz) are one click. */
const MERGE_GAP = 24
const PAD = 2
const AR_ORDER = 24
const MIN_CONTEXT = 256
/** About −60 dB: below this, jumps in near-silence are not worth touching. */
const ABSOLUTE_FLOOR = 1e-3

/** Second difference: near zero for smooth audio, large where a sample jumps out of line. */
function roughness(channel: Float32Array) {
  const output = new Float32Array(channel.length)
  for (let i = 2; i < channel.length; i++)
    output[i] = Math.abs(channel[i]! - 2 * channel[i - 1]! + channel[i - 2]!)
  return output
}

function localLevels(rough: Float32Array) {
  const blocks = Math.ceil(rough.length / BLOCK)
  const means = new Float32Array(blocks)
  for (let block = 0; block < blocks; block++) {
    const end = Math.min(rough.length, (block + 1) * BLOCK)
    let sum = 0
    for (let i = block * BLOCK; i < end; i++) sum += rough[i]!
    means[block] = sum / (end - block * BLOCK)
  }
  const median = (from: number, to: number) => {
    const window = Array.from(means.subarray(Math.max(0, from), Math.min(blocks, to))).sort(
      (a, b) => a - b,
    )
    return window.length > 0 ? window[Math.floor(window.length / 2)]! : 0
  }
  // The louder side wins, so the first "t" after a pause is measured against the speech after it
  // rather than the silence before it.
  return Float32Array.from({ length: blocks }, (_, block) =>
    Math.max(median(block - NEIGHBOURS, block), median(block + 1, block + 1 + NEIGHBOURS)),
  )
}

/** Half-open frame ranges of each click, already padded and merged. */
export function detectClicks(channel: Float32Array, sampleRate: number, options: ClickOptions) {
  const rough = roughness(channel)
  const levels = localLevels(rough)
  const maxLength = Math.max(4, Math.round((options.maxClickMs / 1000) * sampleRate))
  const clicks: { start: number; end: number }[] = []
  let start = -1
  let last = -1
  const close = () => {
    if (start < 0) return
    const range = {
      start: Math.max(0, start - 2 - PAD),
      end: Math.min(channel.length, last + 1 + PAD),
    }
    start = -1
    const previous = clicks.at(-1)
    if (previous && range.start <= previous.end) previous.end = range.end
    else clicks.push(range)
  }
  for (let i = 2; i < rough.length; i++) {
    const bar = options.threshold * levels[Math.floor(i / BLOCK)]! + ABSOLUTE_FLOOR
    if (rough[i]! <= bar) continue
    if (start >= 0 && i - last > MERGE_GAP) close()
    if (start < 0) start = i
    last = i
  }
  close()
  return clicks.filter((click) => click.end - click.start <= maxLength)
}

/** Autoregressive coefficients (a[0] = 1) fitted to the given stretches by Levinson-Durbin. */
function fitAr(segments: Float32Array[], order: number) {
  const r = new Float64Array(order + 1)
  for (const segment of segments)
    for (let lag = 0; lag <= order; lag++)
      for (let i = lag; i < segment.length; i++) r[lag]! += segment[i]! * segment[i - lag]!
  r[0]! = r[0]! * (1 + 1e-9) + 1e-12
  const a = new Float64Array(order + 1)
  a[0] = 1
  let error = r[0]!
  for (let i = 1; i <= order; i++) {
    let acc = r[i]!
    for (let j = 1; j < i; j++) acc += a[j]! * r[i - j]!
    const k = -acc / error
    const previous = a.slice()
    for (let j = 1; j < i; j++) a[j] = previous[j]! + k * previous[i - j]!
    a[i] = k
    error *= 1 - k * k
    if (error <= 0) break
  }
  return a
}

/** Solves the symmetric positive-definite system in place by Cholesky; returns false if singular. */
function solveSpd(matrix: Float64Array, rhs: Float64Array, size: number) {
  for (let j = 0; j < size; j++) {
    let diagonal = matrix[j * size + j]!
    for (let k = 0; k < j; k++) diagonal -= matrix[j * size + k]! ** 2
    if (diagonal <= 0) return false
    const root = Math.sqrt(diagonal)
    matrix[j * size + j] = root
    for (let i = j + 1; i < size; i++) {
      let value = matrix[i * size + j]!
      for (let k = 0; k < j; k++) value -= matrix[i * size + k]! * matrix[j * size + k]!
      matrix[i * size + j] = value / root
    }
  }
  for (let i = 0; i < size; i++) {
    let value = rhs[i]!
    for (let k = 0; k < i; k++) value -= matrix[i * size + k]! * rhs[k]!
    rhs[i] = value / matrix[i * size + i]!
  }
  for (let i = size - 1; i >= 0; i--) {
    let value = rhs[i]!
    for (let k = i + 1; k < size; k++) value -= matrix[k * size + i]! * rhs[k]!
    rhs[i] = value / matrix[i * size + i]!
  }
  return true
}

/**
 * Replaces output[start, end) with the samples that best continue the AR model of the audio on
 * both sides. Returns false (leaving the samples alone) where there is too little context.
 */
function interpolate(output: Float32Array, start: number, end: number) {
  const length = end - start
  const context = Math.max(MIN_CONTEXT, 8 * length)
  const blockStart = Math.max(0, start - context)
  const blockEnd = Math.min(output.length, end + context)
  if (start - blockStart < 2 * AR_ORDER || blockEnd - end < 2 * AR_ORDER) return false

  const a = fitAr([output.subarray(blockStart, start), output.subarray(end, blockEnd)], AR_ORDER)
  const p = AR_ORDER
  // Prediction error with the gap set to zero, wherever a gap sample would feed into it.
  const known = new Float64Array(length + p)
  for (let offset = 0; offset < length + p; offset++) {
    const n = start + offset
    let sum = 0
    for (let k = 0; k <= p; k++) {
      const index = n - k
      if (index >= start && index < end) continue
      sum += a[k]! * (output[index] ?? 0)
    }
    known[offset] = sum
  }
  const autocorrelation = new Float64Array(p + 1)
  for (let lag = 0; lag <= p; lag++)
    for (let k = 0; k + lag <= p; k++) autocorrelation[lag]! += a[k]! * a[k + lag]!
  const matrix = new Float64Array(length * length)
  for (let i = 0; i < length; i++)
    for (let j = 0; j < length; j++) {
      const lag = Math.abs(i - j)
      matrix[i * length + j] = lag <= p ? autocorrelation[lag]! : 0
    }
  const rhs = new Float64Array(length)
  for (let i = 0; i < length; i++) {
    let sum = 0
    for (let m = 0; m <= p; m++) sum += a[m]! * known[i + m]!
    rhs[i] = -sum
  }
  if (!solveSpd(matrix, rhs, length)) return false
  for (let i = 0; i < length; i++) output[start + i] = rhs[i]!
  return true
}

/**
 * Repairs every click it finds, only inside `range` when given; `clicks` counts the repairs across
 * all channels.
 */
export function removeClicks(
  channels: Channels,
  sampleRate: number,
  options: ClickOptions,
  range?: FrameRange | null,
) {
  let clicks = 0
  const output = channels.map((channel) => {
    const repaired = channel.slice()
    for (const click of detectClicks(channel, sampleRate, options)) {
      if (range && (click.start < range.start || click.end > range.end)) continue
      if (interpolate(repaired, click.start, click.end)) clicks++
    }
    return repaired
  })
  return { channels: output, clicks }
}
