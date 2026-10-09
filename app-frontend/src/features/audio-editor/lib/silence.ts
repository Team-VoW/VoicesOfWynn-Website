// Silence trimming that adapts to each recording, like Audition's "Find Levels". A fixed -50 dB
// cut-off trims nothing on a line recorded over -45 dB room noise, and stops at the first noise
// blip on one hovering around -50 dB. Instead: find the file's steady background noise, suggest a
// threshold above it, and detect sound on short RMS windows rather than single samples.

import { SILENCE_THRESHOLD_DB } from '@/features/tools/lib/audioChecks'
import { fade, frameCount, type Channels, type FrameRange } from './operations'

const WINDOW_SECONDS = 0.01
/**
 * Background noise is what a recording starts or ends on: a steady stretch (at least 100 ms within
 * a 6 dB band) right at the start or end of the file, allowing 50 ms for an editor's fade, and well
 * under the speech. Measured on real voice lines, a steady quiet stretch anywhere else is a breath,
 * a held word ending or a reverb tail, and treating those as noise would cut them off.
 */
const EDGE_SECONDS = 0.05
const STEADY_SECONDS = 0.1
const STEADY_RANGE_DB = 6
const NOISE_BELOW_SPEECH_DB = 15
/** Sound has to last this long to count, so one click does not end the silence. */
const MIN_SOUND_SECONDS = 0.02
const FADE_SECONDS = 0.005

/**
 * Never suggest below the VoW rule: the Audio check counts everything under -50 dB as silence, so a
 * lower threshold would keep breaths and reverb tails that still fail its lead/tail limits. The
 * adaptive part only ever raises the threshold, above rooms too noisy for -50 dB to work.
 */
const LOWEST_THRESHOLD_DB = SILENCE_THRESHOLD_DB
const ABOVE_FLOOR_DB = 10
const BELOW_SPEECH_DB = 25
const SILENCE_DB = -120

export interface SilenceProfile {
  /** Level of the steady background (room, mic, gate); null when the line never goes quiet. */
  noiseFloorDb: number | null
  /** Steady level the file starts and ends on (well under the speech); null when it has none. */
  startDb: number | null
  endDb: number | null
  /** Level of the loud parts of the line. */
  speechDb: number
  /** Threshold the trim uses unless someone overrides it. */
  suggestedThresholdDb: number
}

export interface TrimOptions {
  thresholdDb: number
  keepLeadSeconds: number
  keepTailSeconds: number
}

export interface TrimPlan {
  /** Frames to keep. */
  keep: FrameRange
  removedLeadSeconds: number
  removedTailSeconds: number
}

function percentile(sorted: Float32Array, fraction: number) {
  if (sorted.length === 0) return SILENCE_DB
  return sorted[Math.min(sorted.length - 1, Math.floor(fraction * sorted.length))]!
}

const levelCache = new WeakMap<
  Channels,
  { sampleRate: number; levels: Float32Array; size: number }
>()

/**
 * RMS level in dB of each 10 ms window, the loudest channel winning. Remembered per buffer (audio
 * is never changed in place), so the Silence tab does not re-read a 2-hour take on every redraw.
 * Callers must not write to the returned levels.
 */
export function windowLevels(channels: Channels, sampleRate: number) {
  const cached = levelCache.get(channels)
  if (cached?.sampleRate === sampleRate) return cached
  const result = { sampleRate, ...measureWindows(channels, sampleRate) }
  levelCache.set(channels, result)
  return result
}

function measureWindows(channels: Channels, sampleRate: number) {
  const size = Math.max(1, Math.round(sampleRate * WINDOW_SECONDS))
  const frames = frameCount(channels)
  const count = Math.ceil(frames / size)
  const levels = new Float32Array(count).fill(SILENCE_DB)
  for (const channel of channels) {
    for (let window = 0; window < count; window++) {
      const start = window * size
      const end = Math.min(frames, start + size)
      let sum = 0
      for (let i = start; i < end; i++) sum += channel[i]! * channel[i]!
      const db = sum > 0 ? 10 * Math.log10(sum / (end - start)) : SILENCE_DB
      if (db > levels[window]!) levels[window] = Math.max(SILENCE_DB, db)
    }
  }
  return { levels, size }
}

/** The quietest steady stretch starting at one of `starts`, as its mean level. */
function quietestSteadyStretch(levels: Float32Array, starts: number[]) {
  const span = Math.max(1, Math.round(STEADY_SECONDS / WINDOW_SECONDS))
  let floor: number | null = null
  for (const start of starts) {
    if (start < 0 || start + span > levels.length) continue
    let min = Infinity,
      max = -Infinity,
      sum = 0
    for (let i = start; i < start + span; i++) {
      min = Math.min(min, levels[i]!)
      max = Math.max(max, levels[i]!)
      sum += levels[i]!
    }
    if (max - min <= STEADY_RANGE_DB && (floor === null || sum / span < floor)) floor = sum / span
  }
  return floor
}

export function profileSilence(channels: Channels, sampleRate: number): SilenceProfile {
  const { levels } = windowLevels(channels, sampleRate)
  const sorted = levels.slice().sort()
  const speechDb = percentile(sorted, 0.95)

  const edge = Math.max(1, Math.round(EDGE_SECONDS / WINDOW_SECONDS))
  const span = Math.max(1, Math.round(STEADY_SECONDS / WINDOW_SECONDS))
  const offsets = Array.from({ length: edge + 1 }, (_, i) => i)
  const underSpeech = (level: number | null) =>
    level !== null && speechDb - level >= NOISE_BELOW_SPEECH_DB ? level : null
  const startDb = underSpeech(quietestSteadyStretch(levels, offsets))
  const endDb = underSpeech(
    quietestSteadyStretch(
      levels,
      offsets.map((i) => levels.length - span - i),
    ),
  )
  const candidates = [startDb, endDb].filter((level): level is number => level !== null)
  const noiseFloorDb = candidates.length ? Math.min(...candidates) : null

  // Raised over a noisy room, capped under the speech, and never below the VoW rule.
  const suggestedThresholdDb = Math.max(
    Math.min(
      noiseFloorDb === null ? LOWEST_THRESHOLD_DB : noiseFloorDb + ABOVE_FLOOR_DB,
      speechDb - BELOW_SPEECH_DB,
    ),
    LOWEST_THRESHOLD_DB,
  )
  return { noiseFloorDb, startDb, endDb, speechDb, suggestedThresholdDb }
}

export interface LoudEdge {
  edge: 'start' | 'end'
  db: number
}

/**
 * The edges the line starts or ends on a steady sound louder than the -50 dB the Audio check calls
 * silence. Room noise, a held breath and a word cut off at the edge all look like this; either way
 * the check counts it as sound, so its silence figures cannot flag it and someone should listen.
 */
export function loudEdges(profile: SilenceProfile): LoudEdge[] {
  const edges: LoudEdge[] = []
  if (profile.startDb !== null && profile.startDb > SILENCE_THRESHOLD_DB)
    edges.push({ edge: 'start', db: profile.startDb })
  if (profile.endDb !== null && profile.endDb > SILENCE_THRESHOLD_DB)
    edges.push({ edge: 'end', db: profile.endDb })
  return edges
}

export function edgeSoundAboveSilenceRule(profile: SilenceProfile) {
  return loudEdges(profile).length > 0
}

const roundDb = (db: number) => `${Math.round(db)}`.replace('-', '−')

/** "Ends on −42 dB sound", for a short label. Empty when both edges are silent. */
export function describeLoudEdges(profile: SilenceProfile) {
  const edges = loudEdges(profile)
  if (edges.length === 2) {
    const loudest = Math.max(edges[0]!.db, edges[1]!.db)
    return `Both ends on ${roundDb(loudest)} dB sound`
  }
  const [only] = edges
  if (!only) return ''
  return `${only.edge === 'start' ? 'Starts' : 'Ends'} on ${roundDb(only.db)} dB sound`
}

/** The full explanation for a tooltip. */
export function explainLoudEdges(profile: SilenceProfile) {
  const edges = loudEdges(profile)
  if (edges.length === 0) return ''
  const where =
    edges.length === 2
      ? 'The first and last 100 ms hold'
      : edges[0]!.edge === 'start'
        ? 'The first 100 ms hold'
        : 'The last 100 ms hold'
  const loudest = Math.max(...edges.map((edge) => edge.db))
  return `${where} a steady ${roundDb(loudest)} dB level rather than silence, which usually means room noise, hum or a held breath. Not against the VoW rules (starting or ending right on the line is fine), but the Audio check counts it as sound, so listen to the ${edges.length === 2 ? 'start and end' : edges[0]!.edge}.`
}

/** Where the trim would cut. Null when nothing rises above the threshold: never trim to nothing. */
export function planTrim(
  channels: Channels,
  sampleRate: number,
  options: TrimOptions,
): TrimPlan | null {
  const frames = frameCount(channels)
  const { levels, size } = windowLevels(channels, sampleRate)
  const run = Math.max(1, Math.round(MIN_SOUND_SECONDS / WINDOW_SECONDS))
  const loud = (window: number) => levels[window]! > options.thresholdDb

  let first = -1
  for (let window = 0; window + run <= levels.length && first < 0; window++) {
    let sustained = true
    for (let k = 0; k < run && sustained; k++) sustained = loud(window + k)
    if (sustained) first = window
  }
  if (first < 0) return null
  let last = first
  for (let window = levels.length - 1; window >= first + run - 1; window--) {
    let sustained = true
    for (let k = 0; k < run && sustained; k++) sustained = loud(window - k)
    if (sustained) {
      last = window
      break
    }
  }

  const soundStart = first * size
  const soundEnd = Math.min(frames, (last + 1) * size)
  const start = Math.max(0, soundStart - Math.round(options.keepLeadSeconds * sampleRate))
  const end = Math.min(frames, soundEnd + Math.round(options.keepTailSeconds * sampleRate))
  return {
    keep: { start, end },
    removedLeadSeconds: start / sampleRate,
    removedTailSeconds: (frames - end) / sampleRate,
  }
}

/** Cuts to the plan, with a 5 ms fade on each side that was cut so trimming into noise cannot click. */
export function applyTrim(channels: Channels, sampleRate: number, plan: TrimPlan): Channels {
  const { start, end } = plan.keep
  let output = channels.map((channel) => channel.slice(start, end))
  const fadeFrames = Math.min(Math.round(FADE_SECONDS * sampleRate), Math.floor((end - start) / 2))
  if (fadeFrames > 1 && start > 0) output = fade(output, { start: 0, end: fadeFrames }, 'in')
  if (fadeFrames > 1 && end < frameCount(channels))
    output = fade(output, { start: end - start - fadeFrames, end: end - start }, 'out')
  return output
}
