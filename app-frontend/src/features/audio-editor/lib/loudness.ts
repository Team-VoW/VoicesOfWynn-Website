// Loudness per ITU-R BS.1770-4 / EBU R128, the same measure ffmpeg's `ebur128` filter reports on
// the Audio check page, plus 4x oversampled true peak and the head/tail silence that page checks.

import { SILENCE_THRESHOLD_DB } from '@/features/tools/lib/audioChecks'
import {
  type Channels,
  dbToGain,
  frameCount,
  gain,
  gainToDb,
  samplePeak,
  soundBounds,
} from './operations'
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

function filter(input: ArrayLike<number>, { b0, b1, b2, a1, a2 }: Biquad) {
  const output = new Float64Array(input.length)
  let x1 = 0,
    x2 = 0,
    y1 = 0,
    y2 = 0
  for (let i = 0; i < input.length; i++) {
    const x = input[i]!
    const y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
    output[i] = y
    x2 = x1
    x1 = x
    y2 = y1
    y1 = y
  }
  return output
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
  const [shelf, highPass] = kWeighting(sampleRate)

  if (subBlocks < 4) {
    if (frames === 0) return -Infinity
    let power = 0
    for (const channel of channels) {
      const weighted = filter(filter(channel, shelf), highPass)
      for (let i = 0; i < frames; i++) power += weighted[i]! * weighted[i]!
    }
    const lufs = powerToLufs(power / frames)
    return lufs > ABSOLUTE_GATE ? lufs : -Infinity
  }

  // Mean square of each 100 ms sub-block, summed over channels (all weighted 1.0: mono/stereo).
  const energy = new Float64Array(subBlocks)
  for (const channel of channels) {
    const weighted = filter(filter(channel, shelf), highPass)
    for (let block = 0; block < subBlocks; block++) {
      let sum = 0
      for (let i = block * step, end = i + step; i < end; i++) sum += weighted[i]! * weighted[i]!
      energy[block]! += sum
    }
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
const MAX_BRANCH_GAIN = Math.max(
  ...PHASES.map((branch) => branch.reduce((sum, value) => sum + Math.abs(value), 0)),
)
const HALF = TAPS_PER_PHASE / 2

/** Highest absolute value of the 4x oversampled signal around one frame. */
function oversampledPeakAt(channel: Float32Array, frame: number) {
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
 * Feeds the limiter's gain computer, which needs every frame.
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

const BLOCK = 16

/**
 * Same result as the maximum of the envelope, much faster: an interpolated value can never exceed
 * the loudest sample feeding it times the branch gain, so any block whose neighbourhood cannot beat
 * the peak found so far is skipped without filtering. Voice lines are mostly quiet relative to
 * their peak, so only a few percent of the samples get oversampled.
 */
export function truePeak(channels: Channels) {
  let best = samplePeak(channels)
  for (const channel of channels) {
    const blocks = Math.ceil(channel.length / BLOCK)
    const blockMax = new Float32Array(blocks)
    for (let i = 0; i < channel.length; i++) {
      const magnitude = Math.abs(channel[i]!)
      const block = (i / BLOCK) | 0
      if (magnitude > blockMax[block]!) blockMax[block] = magnitude
    }
    for (let block = 0; block < blocks; block++) {
      // A frame in this block reads samples from at most the neighbouring blocks.
      const nearby = Math.max(blockMax[block]!, blockMax[block - 1] ?? 0, blockMax[block + 1] ?? 0)
      if (nearby * MAX_BRANCH_GAIN <= best) continue
      const end = Math.min(channel.length, (block + 1) * BLOCK)
      for (let frame = block * BLOCK; frame < end; frame++) {
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

/**
 * Lookahead brickwall limiter on the true-peak envelope. The gain curve is a running minimum over
 * ±lookahead, smoothed by a release filter and then a moving average of the lookahead length, which
 * keeps it at or below the required gain on every frame while starting to duck before the peak.
 */
export function limit(channels: Channels, sampleRate: number, ceilingDb: number) {
  const frames = frameCount(channels)
  const ceiling = dbToGain(ceilingDb)
  const envelope = truePeakEnvelope(channels)
  const required = new Float32Array(frames)
  for (let i = 0; i < frames; i++) required[i] = envelope[i]! > ceiling ? ceiling / envelope[i]! : 1

  const lookahead = Math.max(1, Math.round(sampleRate * 0.0015))
  // Running minimum over [i - lookahead, i + lookahead] with a monotonic deque: O(n) instead of
  // rescanning the whole window for every frame.
  const held = new Float32Array(frames)
  const deque = new Int32Array(frames)
  let head = 0
  let tail = 0
  for (let j = 0; j < frames + lookahead; j++) {
    if (j < frames) {
      while (tail > head && required[deque[tail - 1]!]! >= required[j]!) tail--
      deque[tail++] = j
    }
    const i = j - lookahead
    if (i < 0) continue
    while (deque[head]! < i - lookahead) head++
    held[i] = Math.min(1, required[deque[head]!]!)
  }

  const release = 1 - Math.exp(-1 / (sampleRate * 0.05))
  const smoothed = new Float32Array(frames)
  let level = 1
  for (let i = 0; i < frames; i++) {
    level = held[i]! < level ? held[i]! : level + (held[i]! - level) * release
    smoothed[i] = level
  }

  const window = Math.max(1, Math.floor(lookahead / 2))
  const curve = new Float32Array(frames)
  let sum = 0
  let count = 0
  for (let i = 0; i < Math.min(frames, window + 1); i++) {
    sum += smoothed[i]!
    count++
  }
  for (let i = 0; i < frames; i++) {
    curve[i] = sum / count
    const leaving = i - window
    const entering = i + window + 1
    if (leaving >= 0) {
      sum -= smoothed[leaving]!
      count--
    }
    if (entering < frames) {
      sum += smoothed[entering]!
      count++
    }
  }

  return channels.map((channel) => {
    const output = new Float32Array(frames)
    for (let i = 0; i < frames; i++) output[i] = channel[i]! * curve[i]!
    return output
  })
}

export interface LoudnessMatch {
  channels: Channels
  before: AudioAnalysis
  after: AudioAnalysis
  gainDb: number
  limited: boolean
}

/** Limits repeatedly, a little lower each time, until the true peak sits under the ceiling. */
function limitUnder(channels: Channels, sampleRate: number, ceilingDbtp: number) {
  let output = channels
  // The limiter tracks a filtered peak estimate, so it can land a hair over; retry a little lower.
  // Strictly under: the Audio check fails anything above the ceiling, even by 0.005 dB.
  for (let attempt = 0; attempt < 4 && truePeak(output) > ceilingDbtp; attempt++)
    output = limit(output, sampleRate, ceilingDbtp - 0.1 * (attempt + 1))
  return output
}

/**
 * What Match loudness will do to a measured file: the plain gain it needs, and whether that gain
 * would push the true peak past the ceiling so the limiter has to step in. Null when silent.
 */
export function planMatch(analysis: AudioAnalysis, targetLufs: number, ceilingDbtp: number) {
  if (!Number.isFinite(analysis.integratedLufs)) return null
  const gainDb = targetLufs - analysis.integratedLufs
  const peakAfterDbtp = analysis.truePeakDbtp + gainDb
  return { gainDb, peakAfterDbtp, needsLimiting: peakAfterDbtp > ceilingDbtp }
}

/**
 * Audition's Match Loudness: gain to the target, then limit only if that pushed the true peak over
 * the ceiling. Limiting removes loudness, so the gain is raised and the line limited again until
 * it lands on the target (or stops getting closer). `after` reports what actually came out.
 */
export function matchLoudness(
  channels: Channels,
  sampleRate: number,
  targetLufs: number,
  ceilingDbtp: number,
  before: AudioAnalysis = analyze(channels, sampleRate),
): LoudnessMatch {
  if (!Number.isFinite(before.integratedLufs))
    return {
      channels: channels.map((c) => c.slice()),
      before,
      after: before,
      gainDb: 0,
      limited: false,
    }

  let gainDb = targetLufs - before.integratedLufs
  let output: Channels = gain(channels, gainDb)
  let limited = false
  for (let pass = 0; pass < 4 && truePeak(output) > ceilingDbtp; pass++) {
    output = limitUnder(output, sampleRate, ceilingDbtp)
    limited = true
    const shortfall = targetLufs - integratedLoudness(output, sampleRate)
    if (shortfall < 0.1) break
    gainDb += shortfall
    output = gain(channels, gainDb)
  }
  if (truePeak(output) > ceilingDbtp) output = limitUnder(output, sampleRate, ceilingDbtp)
  return { channels: output, before, after: analyze(output, sampleRate), gainDb, limited }
}
