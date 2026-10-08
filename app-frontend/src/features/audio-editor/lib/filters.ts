// Filter-based cleanup effects: rumble, hum, EQ, sibilance and dynamics. Biquads follow the RBJ
// Audio EQ Cookbook. Every function returns new arrays and leaves its input alone.

import { dbToGain, type Channels } from './operations'

interface Biquad {
  b0: number
  b1: number
  b2: number
  a1: number
  a2: number
}

type BiquadType = 'highpass' | 'lowpass' | 'peaking' | 'lowshelf' | 'highshelf'

export interface EqBand {
  type: 'lowshelf' | 'peaking' | 'highshelf'
  frequency: number
  gainDb: number
  q: number
}

export interface DeEssOptions {
  /** Sibilance is everything above this frequency. */
  frequency: number
  thresholdDb: number
  maxReductionDb: number
}

export interface CompressorOptions {
  thresholdDb: number
  ratio: number
  attackMs: number
  releaseMs: number
  kneeDb: number
  makeupDb: number
}

function clampFrequency(frequency: number, sampleRate: number) {
  return Math.max(1, Math.min(sampleRate * 0.49, frequency))
}

function biquad(
  type: BiquadType,
  frequency: number,
  sampleRate: number,
  q: number,
  gainDb = 0,
): Biquad {
  const w0 = (2 * Math.PI * clampFrequency(frequency, sampleRate)) / sampleRate
  const cos = Math.cos(w0)
  const alpha = Math.sin(w0) / (2 * q)
  const A = Math.pow(10, gainDb / 40)
  const rootA2Alpha = 2 * Math.sqrt(A) * alpha
  let b0: number, b1: number, b2: number, a0: number, a1: number, a2: number
  switch (type) {
    case 'highpass':
      b0 = (1 + cos) / 2
      b1 = -(1 + cos)
      b2 = (1 + cos) / 2
      a0 = 1 + alpha
      a1 = -2 * cos
      a2 = 1 - alpha
      break
    case 'lowpass':
      b0 = (1 - cos) / 2
      b1 = 1 - cos
      b2 = (1 - cos) / 2
      a0 = 1 + alpha
      a1 = -2 * cos
      a2 = 1 - alpha
      break
    case 'peaking':
      b0 = 1 + alpha * A
      b1 = -2 * cos
      b2 = 1 - alpha * A
      a0 = 1 + alpha / A
      a1 = -2 * cos
      a2 = 1 - alpha / A
      break
    case 'lowshelf':
      b0 = A * (A + 1 - (A - 1) * cos + rootA2Alpha)
      b1 = 2 * A * (A - 1 - (A + 1) * cos)
      b2 = A * (A + 1 - (A - 1) * cos - rootA2Alpha)
      a0 = A + 1 + (A - 1) * cos + rootA2Alpha
      a1 = -2 * (A - 1 + (A + 1) * cos)
      a2 = A + 1 + (A - 1) * cos - rootA2Alpha
      break
    case 'highshelf':
      b0 = A * (A + 1 + (A - 1) * cos + rootA2Alpha)
      b1 = -2 * A * (A - 1 + (A + 1) * cos)
      b2 = A * (A + 1 + (A - 1) * cos - rootA2Alpha)
      a0 = A + 1 - (A - 1) * cos + rootA2Alpha
      a1 = 2 * (A - 1 - (A + 1) * cos)
      a2 = A + 1 - (A - 1) * cos - rootA2Alpha
      break
  }
  return { b0: b0 / a0, b1: b1 / a0, b2: b2 / a0, a1: a1 / a0, a2: a2 / a0 }
}

/** Runs the filters in series over one channel (transposed direct form II, double precision). */
function runBiquads(input: Float32Array, filters: Biquad[]) {
  let output = input
  for (const { b0, b1, b2, a1, a2 } of filters) {
    const source = output
    output = new Float32Array(source.length)
    let z1 = 0
    let z2 = 0
    for (let i = 0; i < source.length; i++) {
      const x = source[i]!
      const y = b0 * x + z1
      z1 = b1 * x - a1 * y + z2
      z2 = b2 * x - a2 * y
      output[i] = y
    }
  }
  return output === input ? input.slice() : output
}

/** The Q of each biquad section of a Butterworth filter of the given (even) order. */
function butterworthQs(order: number) {
  return Array.from(
    { length: order / 2 },
    (_, k) => 1 / (2 * Math.cos(((2 * k + 1) * Math.PI) / (2 * order))),
  )
}

/** Removes rumble, handling noise and plosive thumps below `frequency`. Slope is 12, 24 or 48. */
export function highPass(
  channels: Channels,
  sampleRate: number,
  frequency: number,
  slopeDbPerOctave: 12 | 24 | 48 = 24,
) {
  const filters = butterworthQs(slopeDbPerOctave / 6).map((q) =>
    biquad('highpass', frequency, sampleRate, q),
  )
  return channels.map((channel) => runBiquads(channel, filters))
}

/**
 * Narrow cuts at the mains frequency and its harmonics, like Audition's DeHummer. Q 15 is about
 * 3 Hz wide at 50 Hz, enough to catch a drifting hum without thinning the voice.
 */
export function removeHum(
  channels: Channels,
  sampleRate: number,
  baseFrequency: 50 | 60,
  harmonics: number,
  reductionDb: number,
) {
  const filters: Biquad[] = []
  for (let k = 1; k <= harmonics && baseFrequency * k < sampleRate * 0.45; k++)
    filters.push(biquad('peaking', baseFrequency * k, sampleRate, 15, -Math.abs(reductionDb)))
  return channels.map((channel) => runBiquads(channel, filters))
}

function eqFilters(bands: EqBand[], sampleRate: number) {
  return bands
    .filter((band) => band.gainDb !== 0)
    .map((band) => biquad(band.type, band.frequency, sampleRate, band.q, band.gainDb))
}

export function equalize(channels: Channels, sampleRate: number, bands: EqBand[]) {
  const filters = eqFilters(bands, sampleRate)
  return channels.map((channel) => runBiquads(channel, filters))
}

/** The EQ's gain in dB at each frequency, from the same biquads `equalize` runs. */
export function eqResponseDb(bands: EqBand[], sampleRate: number, frequencies: ArrayLike<number>) {
  const filters = eqFilters(bands, sampleRate)
  return Float32Array.from({ length: frequencies.length }, (_, index) => {
    const w = (2 * Math.PI * frequencies[index]!) / sampleRate
    const cos1 = Math.cos(w)
    const sin1 = Math.sin(w)
    const cos2 = Math.cos(2 * w)
    const sin2 = Math.sin(2 * w)
    let db = 0
    for (const { b0, b1, b2, a1, a2 } of filters) {
      // |H(e^jw)|² with z^-1 = cos w - j sin w.
      const numeratorReal = b0 + b1 * cos1 + b2 * cos2
      const numeratorImaginary = -(b1 * sin1 + b2 * sin2)
      const denominatorReal = 1 + a1 * cos1 + a2 * cos2
      const denominatorImaginary = -(a1 * sin1 + a2 * sin2)
      db +=
        10 *
        Math.log10(
          (numeratorReal ** 2 + numeratorImaginary ** 2) /
            (denominatorReal ** 2 + denominatorImaginary ** 2),
        )
    }
    return db
  })
}

function coefficient(ms: number, sampleRate: number) {
  return ms <= 0 ? 0 : Math.exp(-1 / ((ms / 1000) * sampleRate))
}

/** Splits each channel into below / above `frequency`; the two always sum back to the input. */
function splitBands(channels: Channels, sampleRate: number, frequency: number) {
  const lowPass = [biquad('lowpass', frequency, sampleRate, Math.SQRT1_2)]
  const low = channels.map((channel) => runBiquads(channel, lowPass))
  const high = channels.map((channel, index) => {
    const output = new Float32Array(channel.length)
    for (let i = 0; i < channel.length; i++) output[i] = channel[i]! - low[index]![i]!
    return output
  })
  return { low, high }
}

/** Fast-attack peak envelope of the loudest channel, in linear amplitude. */
function linkedEnvelope(
  channels: Channels,
  sampleRate: number,
  attackMs: number,
  releaseMs: number,
) {
  const frames = channels[0]?.length ?? 0
  const attack = coefficient(attackMs, sampleRate)
  const release = coefficient(releaseMs, sampleRate)
  const envelope = new Float32Array(frames)
  let level = 0
  for (let i = 0; i < frames; i++) {
    let peak = 0
    for (const channel of channels) peak = Math.max(peak, Math.abs(channel[i]!))
    const k = peak > level ? attack : release
    level = k * level + (1 - k) * peak
    envelope[i] = level
  }
  return envelope
}

/** Detection uses a steep high-pass, so a loud low voice does not trigger the de-esser. */
function sibilanceEnvelope(channels: Channels, sampleRate: number, frequency: number) {
  return linkedEnvelope(highPass(channels, sampleRate, frequency, 24), sampleRate, 1, 60)
}

/** The loudest the sibilance band gets, to help pick a de-esser threshold. */
export function sibilancePeakDb(channels: Channels, sampleRate: number, frequency: number) {
  const envelope = sibilanceEnvelope(channels, sampleRate, frequency)
  let peak = 0
  for (const value of envelope) peak = Math.max(peak, value)
  return peak > 0 ? 20 * Math.log10(peak) : -Infinity
}

/**
 * Turns down only the band above `frequency`, and only while it is above the threshold, so harsh
 * "s" and "sh" sounds soften without dulling the rest of the voice.
 */
export function deEss(channels: Channels, sampleRate: number, options: DeEssOptions) {
  const { low, high } = splitBands(channels, sampleRate, options.frequency)
  const envelope = sibilanceEnvelope(channels, sampleRate, options.frequency)
  const maxReduction = Math.abs(options.maxReductionDb)
  return channels.map((_, index) => {
    const output = new Float32Array(envelope.length)
    const lowBand = low[index]!
    const highBand = high[index]!
    for (let i = 0; i < output.length; i++) {
      const levelDb = 20 * Math.log10(envelope[i]! + 1e-12)
      const reduction = Math.min(maxReduction, Math.max(0, levelDb - options.thresholdDb))
      output[i] = lowBand[i]! + highBand[i]! * dbToGain(-reduction)
    }
    return output
  })
}

/** Gain reduction in dB the static curve asks for at `levelDb`, with a soft knee. */
export function staticReduction(levelDb: number, options: CompressorOptions) {
  const { thresholdDb: threshold, ratio, kneeDb: knee } = options
  const over = levelDb - threshold
  if (2 * over < -knee) return 0
  if (knee > 0 && 2 * Math.abs(over) <= knee)
    return ((1 - 1 / ratio) * Math.pow(over + knee / 2, 2)) / (2 * knee)
  return over * (1 - 1 / ratio)
}

/** A feed-forward compressor with the channels linked, so stereo images do not wander. */
export function compress(channels: Channels, sampleRate: number, options: CompressorOptions) {
  const frames = channels[0]?.length ?? 0
  const attack = coefficient(options.attackMs, sampleRate)
  const release = coefficient(options.releaseMs, sampleRate)
  const gains = new Float32Array(frames)
  let smoothed = 0
  for (let i = 0; i < frames; i++) {
    let peak = 0
    for (const channel of channels) peak = Math.max(peak, Math.abs(channel[i]!))
    const target = staticReduction(20 * Math.log10(peak + 1e-12), options)
    const k = target > smoothed ? attack : release
    smoothed = k * smoothed + (1 - k) * target
    gains[i] = dbToGain(options.makeupDb - smoothed)
  }
  return channels.map((channel) => {
    const output = new Float32Array(frames)
    for (let i = 0; i < frames; i++) output[i] = channel[i]! * gains[i]!
    return output
  })
}
