// Loudness per ITU-R BS.1770-4 / EBU R128, the same measure ffmpeg's `ebur128` filter reports on
// the Audio check page, plus 4x oversampled true peak and the head/tail silence that page checks.

import { SILENCE_THRESHOLD_DB } from '@/features/tools/lib/audioChecks'
import { type Channels, frameCount, gainToDb, samplePeak, soundBounds } from './operations'
import { profileSilence, type SilenceProfile } from './silence'

interface Biquad {
  b0: number
  b1: number
  b2: number
  a1: number
  a2: number
}

/**
 * The two K-weighting stages, derived for any sample rate the way libebur128 (and so ffmpeg) does,
 * rather than the 48 kHz coefficient table printed in the standard.
 */
function kWeighting(sampleRate: number): [Biquad, Biquad] {
  let f0 = 1681.974450955533
  const G = 3.999843853973347
  let Q = 0.7071752369554196
  let K = Math.tan((Math.PI * f0) / sampleRate)
  const Vh = Math.pow(10, G / 20)
  const Vb = Math.pow(Vh, 0.4996667741545416)
  let a0 = 1 + K / Q + K * K
  const shelf: Biquad = {
    b0: (Vh + (Vb * K) / Q + K * K) / a0,
    b1: (2 * (K * K - Vh)) / a0,
    b2: (Vh - (Vb * K) / Q + K * K) / a0,
    a1: (2 * (K * K - 1)) / a0,
    a2: (1 - K / Q + K * K) / a0,
  }

  f0 = 38.13547087602444
  Q = 0.5003270373238773
  K = Math.tan((Math.PI * f0) / sampleRate)
  a0 = 1 + K / Q + K * K
  const highPass: Biquad = {
    b0: 1,
    b1: -2,
    b2: 1,
    a1: (2 * (K * K - 1)) / a0,
    a2: (1 - K / Q + K * K) / a0,
  }
  return [shelf, highPass]
}

/**
 * Squared K-weighted signal summed over each `step`-frame block (the last one partial), with both
 * stages run as one stream: a full-length buffer per stage would be 2.5 GB for a 2-hour take.
 */
function weightedEnergy(channel: Float32Array, sampleRate: number, step: number) {
  const [s, h] = kWeighting(sampleRate)
  const energy = new Float64Array(Math.ceil(channel.length / step))
  let x1 = 0,
    x2 = 0,
    m1 = 0,
    m2 = 0,
    y1 = 0,
    y2 = 0
  for (let block = 0; block < energy.length; block++) {
    let sum = 0
    for (let i = block * step, end = Math.min(channel.length, i + step); i < end; i++) {
      const x = channel[i]!
      const m = s.b0 * x + s.b1 * x1 + s.b2 * x2 - s.a1 * m1 - s.a2 * m2
      const y = h.b0 * m + h.b1 * m1 + h.b2 * m2 - h.a1 * y1 - h.a2 * y2
      sum += y * y
      x2 = x1
      x1 = x
      m2 = m1
      m1 = m
      y2 = y1
      y1 = y
    }
    energy[block] = sum
  }
  return energy
}

const ABSOLUTE_GATE = -70
const RELATIVE_GATE = -10

function powerToLufs(power: number) {
  return power > 0 ? -0.691 + 10 * Math.log10(power) : -Infinity
}

/**
 * Gated integrated loudness in LUFS. Clips entirely below the absolute gate return -Infinity.
 *
 * BS.1770 has no reading for clips shorter than one 400 ms block (ffmpeg reports its -70 floor).
 * Short barks and grunts are real voice lines that still need matching, so those are measured as
 * a single block over the whole clip, as Audition does.
 */
export function integratedLoudness(channels: Channels, sampleRate: number) {
  const frames = frameCount(channels)
  const step = Math.round(sampleRate * 0.1)
  const subBlocks = Math.floor(frames / step)

  if (subBlocks < 4) {
    if (frames === 0) return -Infinity
    let power = 0
    for (const channel of channels)
      power += weightedEnergy(channel, sampleRate, frames).reduce((sum, value) => sum + value, 0)
    const lufs = powerToLufs(power / frames)
    return lufs > ABSOLUTE_GATE ? lufs : -Infinity
  }

  // Energy of each 100 ms sub-block, summed over channels (all weighted 1.0: mono/stereo).
  const energy = new Float64Array(subBlocks)
  for (const channel of channels) {
    const weighted = weightedEnergy(channel, sampleRate, step)
    for (let block = 0; block < subBlocks; block++) energy[block]! += weighted[block]!
  }

  // 400 ms gating blocks with 75% overlap.
  const blocks: number[] = []
  for (let block = 0; block + 4 <= subBlocks; block++)
    blocks.push(
      (energy[block]! + energy[block + 1]! + energy[block + 2]! + energy[block + 3]!) / (4 * step),
    )

  const aboveAbsolute = blocks.filter((power) => powerToLufs(power) > ABSOLUTE_GATE)
  if (aboveAbsolute.length === 0) return -Infinity
  const mean = (values: number[]) => values.reduce((sum, value) => sum + value, 0) / values.length
  const relativeGate = powerToLufs(mean(aboveAbsolute)) + RELATIVE_GATE
  const gated = aboveAbsolute.filter((power) => powerToLufs(power) > relativeGate)
  return powerToLufs(mean(gated))
}

const OVERSAMPLE = 4
const TAPS_PER_PHASE = 16

/** Kaiser-windowed sinc interpolator split into its four polyphase branches. */
const PHASES: Float64Array[] = (() => {
  const length = OVERSAMPLE * TAPS_PER_PHASE
  const center = (length - 1) / 2
  const beta = 7
  const bessel = (x: number) => {
    let sum = 1,
      term = 1
    for (let k = 1; k < 25; k++) {
      term *= (x / (2 * k)) ** 2
      sum += term
    }
    return sum
  }
  const taps = Array.from({ length }, (_, n) => {
    const t = (n - center) / OVERSAMPLE
    const sinc = t === 0 ? 1 : Math.sin(Math.PI * t) / (Math.PI * t)
    const ratio = (2 * (n - center)) / (length - 1)
    return (sinc * bessel(beta * Math.sqrt(Math.max(0, 1 - ratio * ratio)))) / bessel(beta)
  })
  return Array.from({ length: OVERSAMPLE }, (_, phase) => {
    const branch = new Float64Array(TAPS_PER_PHASE)
    for (let k = 0; k < TAPS_PER_PHASE; k++) branch[k] = taps[k * OVERSAMPLE + phase]!
    const sum = branch.reduce((total, value) => total + value, 0)
    return branch.map((value) => value / sum)
  })
})()

/** Largest gain any branch can apply: bounds an interpolated value by its loudest input sample. */
export const MAX_BRANCH_GAIN = Math.max(
  ...PHASES.map((branch) => branch.reduce((sum, value) => sum + Math.abs(value), 0)),
)
const HALF = TAPS_PER_PHASE / 2
/** How many frames either side of a frame its oversampled peak reads. */
export const TRUE_PEAK_REACH = HALF

/** Highest absolute value of the 4x oversampled signal around one frame. */
export function oversampledPeakAt(channel: Float32Array, frame: number) {
  let peak = Math.abs(channel[frame]!)
  const interior = frame - HALF + 1 >= 0 && frame + HALF < channel.length
  for (const branch of PHASES) {
    let value = 0
    if (interior) {
      for (let k = 0; k < TAPS_PER_PHASE; k++) value += channel[frame + HALF - k]! * branch[k]!
    } else {
      for (let k = 0; k < TAPS_PER_PHASE; k++) {
        const sample = channel[frame + HALF - k]
        if (sample !== undefined) value += sample * branch[k]!
      }
    }
    const magnitude = Math.abs(value)
    if (magnitude > peak) peak = magnitude
  }
  return peak
}

/**
 * The highest absolute value of the 4x oversampled signal around each frame, across channels.
 * The exhaustive reference the block-skipping meters are checked against.
 */
export function truePeakEnvelope(channels: Channels) {
  const frames = frameCount(channels)
  const envelope = new Float32Array(frames)
  for (const channel of channels) {
    for (let frame = 0; frame < frames; frame++) {
      const peak = oversampledPeakAt(channel, frame)
      if (peak > envelope[frame]!) envelope[frame] = peak
    }
  }
  return envelope
}

/** Frames per block of `peakBlocks`; a frame's oversampled peak reads at most its neighbours. */
export const PEAK_BLOCK = 16

/**
 * Loudest sample of each PEAK_BLOCK-frame block across channels. An interpolated value can never
 * exceed the loudest sample feeding it times MAX_BRANCH_GAIN, so a block whose neighbourhood stays
 * under a level needs no oversampling to know its true peak does too.
 */
export function peakBlocks(channels: Channels) {
  const frames = frameCount(channels)
  const blocks = new Float32Array(Math.ceil(frames / PEAK_BLOCK))
  for (const channel of channels) {
    for (let i = 0; i < frames; i++) {
      const magnitude = Math.abs(channel[i]!)
      const block = (i / PEAK_BLOCK) | 0
      if (magnitude > blocks[block]!) blocks[block] = magnitude
    }
  }
  return blocks
}

/** The loudest sample a frame in `block` can read when oversampled. */
export function nearbyPeak(blocks: Float32Array, block: number) {
  return Math.max(blocks[block]!, blocks[block - 1] ?? 0, blocks[block + 1] ?? 0)
}

/**
 * Same result as the maximum of the envelope, much faster: any block whose neighbourhood cannot
 * beat the peak found so far is skipped without filtering. Voice lines are mostly quiet relative
 * to their peak, so only a few percent of the samples get oversampled.
 */
export function truePeak(channels: Channels) {
  let best = samplePeak(channels)
  const blocks = peakBlocks(channels)
  const frames = frameCount(channels)
  for (let block = 0; block < blocks.length; block++) {
    if (nearbyPeak(blocks, block) * MAX_BRANCH_GAIN <= best) continue
    const end = Math.min(frames, (block + 1) * PEAK_BLOCK)
    for (const channel of channels) {
      for (let frame = block * PEAK_BLOCK; frame < end; frame++) {
        const peak = oversampledPeakAt(channel, frame)
        if (peak > best) best = peak
      }
    }
  }
  return gainToDb(best)
}

export interface AudioAnalysis {
  /** Noise floor, speech level and suggested trim threshold. */
  silence: SilenceProfile
  integratedLufs: number
  truePeakDbtp: number
  samplePeakDbfs: number
  leadingSilenceSeconds: number
  trailingSilenceSeconds: number
  durationSeconds: number
}

export function analyze(channels: Channels, sampleRate: number): AudioAnalysis {
  const frames = frameCount(channels)
  const bounds = soundBounds(channels, SILENCE_THRESHOLD_DB)
  return {
    silence: profileSilence(channels, sampleRate),
    integratedLufs: integratedLoudness(channels, sampleRate),
    truePeakDbtp: truePeak(channels),
    samplePeakDbfs: gainToDb(samplePeak(channels)),
    leadingSilenceSeconds: bounds ? bounds.start / sampleRate : frames / sampleRate,
    trailingSilenceSeconds: bounds ? (frames - bounds.end) / sampleRate : frames / sampleRate,
    durationSeconds: frames / sampleRate,
  }
}
