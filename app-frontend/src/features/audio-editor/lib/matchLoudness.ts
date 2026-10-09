// Audition-style Match loudness: gain to the target, with the peaks held under a true-peak
// ceiling. How the peaks are held is the part that decides whether a loud line still sounds clean.

import { compressPeaks, limitTruePeak, type PeakCompressorOptions } from './dynamics'
import { analyze, integratedLoudness, truePeak, type AudioAnalysis } from './loudness'
import { type Channels, gain } from './operations'

/**
 * `auto` adds a gentle compressor in front of the limiter when the limiter would otherwise have
 * to work hard; `limit` uses the limiter alone, as Audition's Match Loudness does.
 */
export type PeakControl = 'auto' | 'limit'

export interface MatchOptions {
  targetLufs: number
  ceilingDbtp: number
  peakControl: PeakControl
}

export interface LoudnessMatch {
  channels: Channels
  before: AudioAnalysis
  after: AudioAnalysis
  gainDb: number
  /** Whether the peaks needed holding down at all. */
  limited: boolean
  /** The most the compressor turned the audio down, in dB. */
  compressionDb: number
  /** The most the limiter turned the audio down, in dB. */
  limitingDb: number
}

/**
 * How far over the ceiling the peaks may go before Auto brings in the compressor. A limiter on
 * this little, a few isolated peaks, is inaudible; much more and it starts to flatten syllables.
 */
export const LIMITER_BUDGET_DB = 2
/** Past this much the limiter is audibly flattening the line, however well it is done. */
export const HEAVY_LIMITING_DB = 8
/** Landing further than this under the target is worth telling the user about. */
export const SHORTFALL_LU = 0.5

/** How close to the target is close enough; well inside the Audio check's ±1 LU. */
const TOLERANCE_LU = 0.05
/** Gain that buys less loudness than this per dB is just squashing. */
const NO_RETURN = 0.1
/** The most gain on top of the plain match that is ever worth pushing into peak control. */
const MAX_EXTRA_GAIN_DB = 12
const MAX_PASSES = 6
/** Our meter and ffmpeg's interpolate differently; stay this far under so the Audio check agrees. */
const CEILING_MARGIN_DB = 0.1

/**
 * What Match loudness will do to a measured file: the plain gain it needs, how far that gain
 * would push the true peak past the ceiling, and whether Auto will compress. Null when silent.
 */
export function planMatch(analysis: AudioAnalysis, options: MatchOptions) {
  if (!Number.isFinite(analysis.integratedLufs)) return null
  const gainDb = options.targetLufs - analysis.integratedLufs
  const peakAfterDbtp = analysis.truePeakDbtp + gainDb
  const reductionDb = Math.max(0, peakAfterDbtp - options.ceilingDbtp)
  return {
    gainDb,
    peakAfterDbtp,
    /**
     * How far the loudest peak has to come down at the plain gain, in dB. A floor: holding peaks
     * down costs loudness, which the match makes up with more gain and so more reduction.
     */
    reductionDb,
    needsLimiting: reductionDb > 0,
    compresses: options.peakControl === 'auto' && reductionDb > LIMITER_BUDGET_DB,
  }
}

/**
 * Auto's compressor: 3:1 from 4 dB under where the ceiling sits at the plain gain, so it eases
 * the loud syllables down by an amount set by how far the line is over. It runs once, before the
 * gain; any further gain the match needs goes to the limiter, which costs far less loudness per
 * dB than compressing harder would.
 */
function compressorFor(plan: { gainDb: number }, options: MatchOptions): PeakCompressorOptions {
  return { thresholdDb: options.ceilingDbtp - 4 - plan.gainDb, ratio: 3, kneeDb: 8 }
}

interface Rendered {
  channels: Channels
  limitingDb: number
}

/** Gain, then the limiter only if the peaks do not fit under the ceiling. */
function render(
  source: Channels,
  sourcePeakDbtp: number,
  sampleRate: number,
  gainDb: number,
  ceilingDbtp: number,
): Rendered {
  const ceiling = ceilingDbtp - CEILING_MARGIN_DB
  if (sourcePeakDbtp + gainDb <= ceiling) return { channels: gain(source, gainDb), limitingDb: 0 }

  const limited = limitTruePeak(source, sampleRate, ceiling, gainDb)
  return { channels: limited.channels, limitingDb: limited.maxReductionDb }
}

/**
 * Gain to the target, holding the peaks under the ceiling. Holding peaks down removes a little
 * loudness, so the gain is raised and the line rendered again until it lands on the target, or
 * more gain would only squash it. Every render starts from the same source (the original, or in
 * Auto the once-compressed original), so nothing is limited twice. `after` reports what actually
 * came out.
 */
export function matchLoudness(
  channels: Channels,
  sampleRate: number,
  options: MatchOptions,
  before: AudioAnalysis = analyze(channels, sampleRate),
): LoudnessMatch {
  const plan = planMatch(before, options)
  if (!plan)
    return {
      channels: channels.map((c) => c.slice()),
      before,
      after: before,
      gainDb: 0,
      limited: false,
      compressionDb: 0,
      limitingDb: 0,
    }

  const finish = (result: Rendered, gainDb: number, compressionDb = 0): LoudnessMatch => ({
    ...result,
    before,
    after: analyze(result.channels, sampleRate),
    gainDb,
    limited: compressionDb > 0 || result.limitingDb > 0,
    compressionDb,
  })

  if (!plan.needsLimiting)
    return finish({ channels: gain(channels, plan.gainDb), limitingDb: 0 }, plan.gainDb)

  const compressed = plan.compresses
    ? compressPeaks(channels, sampleRate, compressorFor(plan, options))
    : null
  const source = compressed?.channels ?? channels
  const sourcePeakDbtp = compressed ? truePeak(source) : before.truePeakDbtp
  const attempt = (gainDb: number) => {
    const result = render(source, sourcePeakDbtp, sampleRate, gainDb, options.ceilingDbtp)
    return { result, gainDb, lufs: integratedLoudness(result.channels, sampleRate) }
  }
  const miss = (lufs: number) => Math.abs(options.targetLufs - lufs)

  // Holding peaks down costs loudness, so solve for the gain that lands on the target. Loudness
  // rises steadily with gain, so a secant search gets there in two or three renders. Only the
  // best render's audio is kept; on a long take each one is a full copy.
  let best = attempt(plan.gainDb)
  let previous = { gainDb: best.gainDb, lufs: best.lufs }
  let current = { gainDb: best.gainDb, lufs: best.lufs }
  for (let pass = 0; pass < MAX_PASSES && miss(best.lufs) > TOLERANCE_LU; pass++) {
    // The first step assumes a dB of gain buys a LU; after that, the measured slope.
    const slope =
      pass === 0 ? 1 : (current.lufs - previous.lufs) / (current.gainDb - previous.gainDb)
    // Gain that buys next to no loudness only squashes: the line is as loud as the ceiling allows.
    if (!(slope > NO_RETURN)) break
    const gainDb = Math.min(
      plan.gainDb + MAX_EXTRA_GAIN_DB,
      current.gainDb + (options.targetLufs - current.lufs) / slope,
    )
    if (Math.abs(gainDb - current.gainDb) < 0.01) break
    const next = attempt(gainDb)
    previous = current
    current = { gainDb: next.gainDb, lufs: next.lufs }
    if (miss(next.lufs) < miss(best.lufs)) best = next
  }
  // The limiter lands on its ceiling, CEILING_MARGIN_DB under the real one, give or take the
  // interpolator. Should a line ever still measure over, render it again a little lower.
  let result = finish(best.result, best.gainDb, compressed?.maxReductionDb ?? 0)
  for (let retry = 1; retry <= 3 && result.after.truePeakDbtp > options.ceilingDbtp; retry++)
    result = finish(
      render(source, sourcePeakDbtp, sampleRate, best.gainDb, options.ceilingDbtp - 0.1 * retry),
      best.gainDb,
      compressed?.maxReductionDb ?? 0,
    )
  return result
}
