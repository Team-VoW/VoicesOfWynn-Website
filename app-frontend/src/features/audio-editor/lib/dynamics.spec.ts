import { describe, expect, it } from 'vitest'
import { compressPeaks, limitTruePeak } from './dynamics'
import { truePeak } from './loudness'
import { dbToGain } from './operations'

const RATE = 48000

function seeded(seed: number) {
  let state = seed
  return () => {
    state = (state * 1664525 + 1013904223) >>> 0
    return (state / 2 ** 32) * 2 - 1
  }
}

/** Two tones mixed 4:1, the SMPTE intermodulation test signal. */
function twoTone(seconds: number, low: number, amplitude = 0.5) {
  return Float32Array.from({ length: Math.round(seconds * RATE) }, (_, i) => {
    const t = i / RATE
    return (
      amplitude * (0.8 * Math.sin(2 * Math.PI * low * t) + 0.2 * Math.sin(2 * Math.PI * 1730 * t))
    )
  })
}

/** Hann-windowed amplitude of one frequency over a stretch of the signal. */
function amplitudeAt(signal: Float32Array, start: number, length: number, frequency: number) {
  let real = 0
  let imaginary = 0
  for (let i = 0; i < length; i++) {
    const weight = 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (length - 1))
    const phase = (2 * Math.PI * frequency * i) / RATE
    real += signal[start + i]! * weight * Math.cos(phase)
    imaginary += signal[start + i]! * weight * Math.sin(phase)
  }
  return Math.hypot(real, imaginary)
}

/** Sidebands the low tone puts on the high one, relative to the high one. */
function intermodulation(signal: Float32Array, low: number) {
  const start = Math.round(0.5 * RATE)
  const length = Math.round(0.5 * RATE)
  let sidebands = 0
  for (let k = 1; k <= 4; k++)
    for (const frequency of [1730 - k * low, 1730 + k * low])
      sidebands += amplitudeAt(signal, start, length, frequency) ** 2
  return Math.sqrt(sidebands) / amplitudeAt(signal, start, length, 1730)
}

describe('limitTruePeak', () => {
  it('never lets the true peak past the ceiling, however hard it is pushed', () => {
    const random = seeded(3)
    for (const gainDb of [0, 6, 12, 24]) {
      // Noise with loud bursts, one of them right at the first frame and one at the last.
      const signal = Float32Array.from({ length: RATE }, () => random() * 0.05)
      for (const at of [0, 9000, 30000, RATE - 40])
        for (let i = at; i < at + 40; i++) signal[i] = random() * 0.9

      const { channels } = limitTruePeak([signal], RATE, -1, gainDb)

      expect(truePeak(channels)).toBeLessThanOrEqual(-1 + 1e-4)
    }
  })

  it('links stereo channels so the image does not shift', () => {
    const left = twoTone(1, 110, 0.9)
    const right = left.map((value) => value * 0.25)

    const { channels } = limitTruePeak([left, right], RATE, -6)

    // Wherever there is signal to compare, the right channel is still a quarter of the left.
    let worst = 0
    for (let i = 0; i < left.length; i++)
      if (Math.abs(left[i]!) > 0.01)
        worst = Math.max(worst, Math.abs(channels[1]![i]! / channels[0]![i]! - 0.25))
    expect(worst).toBeLessThan(1e-5)
  })

  it('leaves audio under the ceiling untouched apart from the input gain', () => {
    const signal = twoTone(1, 110, 0.2)

    const result = limitTruePeak([signal], RATE, -1, 3)

    expect(result.maxReductionDb).toBe(0)
    const gain = dbToGain(3)
    for (let i = 0; i < signal.length; i += 101)
      expect(result.channels[0]![i]).toBeCloseTo(signal[i]! * gain, 6)
  })

  it('holds one steady gain through a loud low voice instead of ducking every wave', () => {
    // 80 Hz is the bottom of a deep male voice; a limiter that recovers between its waves
    // modulates everything above it.
    for (const low of [80, 110]) {
      const { channels, maxReductionDb } = limitTruePeak([twoTone(1, low, 0.9)], RATE, -1, 8)

      expect(maxReductionDb).toBeGreaterThan(8)
      expect(intermodulation(channels[0]!, low)).toBeLessThan(0.0005)
    }
  })

  it('recovers quickly after a lone peak', () => {
    const signal = twoTone(1, 220, 0.1)
    for (let i = RATE / 4; i < RATE / 4 + 20; i++) signal[i] = 0.99

    const [output] = limitTruePeak([signal], RATE, -6).channels

    // About 6 dB of reduction, back to within 0.1 dB of untouched 200 ms later.
    const at = RATE / 4 + Math.round(0.2 * RATE)
    expect(output![at]! / signal[at]!).toBeGreaterThan(dbToGain(-0.1))
    // And it was already down at the peak, not after it.
    expect(Math.abs(output![RATE / 4]!)).toBeLessThanOrEqual(dbToGain(-6) + 1e-6)
  })
})

describe('compressPeaks', () => {
  it('turns loud passages down by the ratio and leaves quiet ones alone', () => {
    const quiet = twoTone(1, 220, 0.05)
    const loud = twoTone(1, 220, 0.5)
    const options = { thresholdDb: -20, ratio: 3, kneeDb: 0 }

    const [quietOut] = compressPeaks([quiet], RATE, options).channels
    const result = compressPeaks([loud], RATE, options)

    const settled = RATE / 2
    expect(quietOut![settled]).toBeCloseTo(quiet[settled]!, 6)
    // The peak level is about −6 dB: 14 dB over the threshold, so 3:1 takes off two thirds.
    expect(result.maxReductionDb).toBeCloseTo(14 * (2 / 3), 0)
  })
})
