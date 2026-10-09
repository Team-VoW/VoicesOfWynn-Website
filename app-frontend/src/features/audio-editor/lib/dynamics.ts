// Peak control for Match loudness: a voice compressor that eases the loud syllables down first,
// then a true-peak limiter that trims whatever still pokes through. Timings were tuned on real
// voice lines and on the two-tone intermodulation test limiters are judged by. Both stream through the audio
// with small ring buffers, so a two-hour take costs one output buffer per channel and no
// frame-length work arrays.

import { staticReduction } from './filters'
import {
  MAX_BRANCH_GAIN,
  PEAK_BLOCK,
  TRUE_PEAK_REACH,
  nearbyPeak,
  oversampledPeakAt,
  peakBlocks,
} from './loudness'
import { type Channels, dbToGain, frameCount, gainToDb } from './operations'

export interface DynamicsResult {
  channels: Channels
  /** The most the audio was turned down at any point, in dB (0 when it never was). */
  maxReductionDb: number
}

/**
 * The smallest of the last `size` values pushed, in O(1) amortised per value: a monotonic deque on
 * a ring buffer, since a window of milliseconds slides over hundreds of millions of frames.
 */
class SlidingMinimum {
  private readonly values: Float64Array
  private readonly positions: Float64Array
  private head = 0
  private count = 0
  private pushed = 0

  constructor(private readonly size: number) {
    this.values = new Float64Array(size + 1)
    this.positions = new Float64Array(size + 1)
  }

  push(value: number) {
    const capacity = this.values.length
    while (this.count > 0) {
      let last = this.head + this.count - 1
      if (last >= capacity) last -= capacity
      if (this.values[last]! < value) break
      this.count--
    }
    let slot = this.head + this.count
    if (slot >= capacity) slot -= capacity
    this.values[slot] = value
    this.positions[slot] = this.pushed
    this.count++
    this.pushed++
    if (this.positions[this.head]! <= this.pushed - 1 - this.size) {
      this.head = this.head + 1 === capacity ? 0 : this.head + 1
      this.count--
    }
    return this.values[this.head]!
  }

  /** Forgets everything pushed so far, for when the whole window is known to be at its ceiling. */
  clear() {
    this.count = 0
  }
}

/** Per-sample factor of a one-pole smoother with time constant `ms`. */
function pole(ms: number, sampleRate: number) {
  return Math.exp(-1 / ((ms / 1000) * sampleRate))
}

function frames(ms: number, sampleRate: number) {
  return Math.max(1, Math.round((ms / 1000) * sampleRate))
}

// The limiter's timing. The lookahead is the attack: the gain glides down over 5 ms in an S-curve
// so it is already at the right level when the peak arrives, with no click. The hold keeps it
// there for a full cycle of a deep voice (down to about 85 Hz), so a loud vowel gets one steady
// gain instead of a duck on every wave, which is what makes a fast limiter sound buzzy. The
// 40 ms release then recovers before a dip can be heard; slower releases were measured to cost
// loudness on speech, which the match then has to buy back with more limiting.
const LIMIT_LOOKAHEAD_MS = 5
const LIMIT_HOLD_MS = 12
const LIMIT_RELEASE_MS = 40

/**
 * Brickwall limiter on the 4x oversampled true peak, channels linked. The gain at every frame is
 * at most what each true peak within reach of it needs, so nothing comes out above the ceiling
 * (to within the interpolator), however far over the input goes.
 *
 * The gain curve is the minimum of the required gain over the hold and lookahead, given a release
 * in dB, then smoothed by two box filters spanning the lookahead. Each
 * stage can only lower the gain, and the box filters only average values that already satisfy
 * the peak, which is what keeps the guarantee while giving an S-shaped attack.
 *
 * `inputGainDb` is applied on the way in, so matching never needs a separate gained copy.
 */
export function limitTruePeak(
  channels: Channels,
  sampleRate: number,
  ceilingDb: number,
  inputGainDb = 0,
): DynamicsResult {
  const total = frameCount(channels)
  const ceiling = dbToGain(ceilingDb)
  const input = dbToGain(inputGainDb)
  const lookahead = frames(LIMIT_LOOKAHEAD_MS, sampleRate)
  // The gain has to be fully down across every sample the interpolator reads around a peak.
  const reach = lookahead + TRUE_PEAK_REACH
  const hold = Math.max(TRUE_PEAK_REACH, frames(LIMIT_HOLD_MS, sampleRate))
  const minimum = new SlidingMinimum(hold + reach + 1)

  const release = pole(LIMIT_RELEASE_MS, sampleRate)

  // Two box filters whose combined span is exactly the lookahead (n1 + n2 - 1 = lookahead + 1).
  const n1 = Math.floor(lookahead / 2) + 1
  const n2 = lookahead + 2 - n1
  const box1 = new Float64Array(n1).fill(1)
  const box2 = new Float64Array(n2).fill(1)
  let sum1 = n1
  let sum2 = n2
  let slot1 = 0
  let slot2 = 0

  // Blocks whose loudest nearby sample cannot reach the ceiling skip the oversampling.
  const blocks = peakBlocks(channels)
  const safe = ceiling / (input * MAX_BRANCH_GAIN)
  const loud = (block: number) => nearbyPeak(blocks, block) > safe

  const outputs = channels.map(() => new Float32Array(total))
  const copy = (from: number, to: number) => {
    for (let c = 0; c < channels.length; c++) {
      const source = channels[c]!
      const output = outputs[c]!
      for (let k = Math.max(0, from); k < Math.min(total, to); k++) output[k] = source[k]! * input
    }
  }
  // Frames since the required gain was last below 1, and since the gain itself was.
  const window = hold + reach + 1
  let quiet = window
  let settled = n1 + n2
  let attenuation = 0
  let lowest = 1

  for (let j = 0; j < total + reach; ) {
    // Idle: nothing to hold down anywhere in reach, and the gain is back at exactly 1. Most of a
    // voice line is like this, so jump straight to the next block that might need the limiter.
    if (quiet >= window && settled >= n1 + n2) {
      let block = (j / PEAK_BLOCK) | 0
      while (block < blocks.length && !loud(block)) block++
      const next = Math.min(total + reach, block * PEAK_BLOCK)
      if (next > j) {
        copy(j - reach, next - reach)
        minimum.clear()
        box1.fill(1)
        box2.fill(1)
        sum1 = n1
        sum2 = n2
        j = next
        continue
      }
    }

    let required = 1
    if (j < total && loud((j / PEAK_BLOCK) | 0)) {
      let peak = 0
      for (const channel of channels) peak = Math.max(peak, oversampledPeakAt(channel, j))
      peak *= input
      if (peak > ceiling) required = ceiling / peak
    }
    quiet = required < 1 ? 0 : quiet + 1
    const held = minimum.push(required)
    // The box filters reach `lookahead` frames back, so the gain computer starts that far before
    // the first frame: a peak right at the start then has the gain down in time like any other.
    const k = j - reach
    j++
    if (k < -lookahead) continue

    // Instant down (the box filters shape the attack), exponential release in dB.
    const target = held < 1 ? -gainToDb(held) : 0
    attenuation = target >= attenuation ? target : target + (attenuation - target) * release
    if (attenuation < 1e-6) attenuation = 0
    const gain = attenuation > 0 ? dbToGain(-attenuation) : 1
    settled = gain < 1 ? 0 : settled + 1

    sum1 += gain - box1[slot1]!
    box1[slot1] = gain
    slot1 = slot1 + 1 === n1 ? 0 : slot1 + 1
    const stage = sum1 / n1
    sum2 += stage - box2[slot2]!
    box2[slot2] = stage
    slot2 = slot2 + 1 === n2 ? 0 : slot2 + 1
    const smoothed = Math.min(1, sum2 / n2)
    if (k < 0) continue

    if (smoothed < lowest) lowest = smoothed
    const factor = input * smoothed
    for (let c = 0; c < channels.length; c++) outputs[c]![k] = channels[c]![k]! * factor
  }

  return { channels: outputs, maxReductionDb: lowest < 1 ? -gainToDb(lowest) : 0 }
}

export interface PeakCompressorOptions {
  thresholdDb: number
  ratio: number
  kneeDb: number
}

// The compressor's timing, set for speech. It sees 5 ms ahead and holds its reading for 12 ms (as
// the limiter does) so it follows syllables rather than individual waves. The 10 ms attack lets
// the first edge of a consonant through for the limiter to catch, which keeps the voice's bite;
// the 150 ms release follows the phrase rather than pumping between words.
const COMPRESS_LOOKAHEAD_MS = 5
const COMPRESS_HOLD_MS = 12
const COMPRESS_ATTACK_MS = 10
const COMPRESS_RELEASE_MS = 150

/**
 * Feed-forward soft-knee compressor for voice, channels linked, smoothed in dB. Meant to sit in
 * front of `limitTruePeak`: it brings the loud passages down smoothly so the limiter only has to
 * shave the odd transient, which is where a limiter alone starts to distort.
 */
export function compressPeaks(
  channels: Channels,
  sampleRate: number,
  options: PeakCompressorOptions,
  inputGainDb = 0,
): DynamicsResult {
  const total = frameCount(channels)
  const input = dbToGain(inputGainDb)
  const lookahead = frames(COMPRESS_LOOKAHEAD_MS, sampleRate)
  const hold = frames(COMPRESS_HOLD_MS, sampleRate)
  // A sliding maximum, as the minimum of the negated levels.
  const maximum = new SlidingMinimum(hold + lookahead + 1)
  const attack = pole(COMPRESS_ATTACK_MS, sampleRate)
  const release = pole(COMPRESS_RELEASE_MS, sampleRate)
  const curve = { ...options, attackMs: 0, releaseMs: 0, makeupDb: 0 }
  const kneeStart = dbToGain(options.thresholdDb - options.kneeDb / 2)

  const outputs = channels.map(() => new Float32Array(total))
  let reduction = 0
  let most = 0

  for (let j = 0; j < total + lookahead; j++) {
    let peak = 0
    if (j < total) for (const channel of channels) peak = Math.max(peak, Math.abs(channel[j]!))
    const level = -maximum.push(-peak * input)
    const k = j - lookahead
    if (k < 0) continue

    // Below the knee there is nothing to compute, which is most of a line.
    const target = level > kneeStart ? staticReduction(gainToDb(level), curve) : 0
    if (target !== 0 || reduction !== 0) {
      reduction = target + (reduction - target) * (target > reduction ? attack : release)
      if (reduction < 1e-6) reduction = 0
    }
    if (reduction > most) most = reduction
    const factor = reduction > 0 ? input * dbToGain(-reduction) : input
    for (let c = 0; c < channels.length; c++) outputs[c]![k] = channels[c]![k]! * factor
  }

  return { channels: outputs, maxReductionDb: most }
}
