// Noise-print noise reduction, as in Audition's Noise Reduction (process): capture what the room
// sounds like from a stretch with no speech, then subtract that spectrum from the whole file.

import { fft } from '@/components/audio/fft'
import { dbToGain, type Channels, type FrameRange } from './operations'

export interface NoisePrint {
  sampleRate: number
  /** Average magnitude per FFT bin, 0 to Nyquist. */
  magnitude: Float32Array
  seconds: number
  source: string
}

export interface NoiseReductionOptions {
  /** The most any bin is turned down, so the voice never sounds gated. */
  reductionDb: number
  /** How far above the print a bin must be to pass untouched; 1 subtracts the print exactly. */
  strength: number
}

const FFT_SIZE = 2048
const HOP = FFT_SIZE / 4
const BINS = FFT_SIZE / 2 + 1
// A periodic Hann window applied on analysis and on synthesis sums to 1.5 at a quarter overlap.
const OVERLAP_GAIN = 1.5
/** Opening a bin follows speech quickly; closing it is slower, which hides "musical" noise. */
const OPEN_RATE = 0.7
const CLOSE_RATE = 0.35

const WINDOW = Float32Array.from(
  { length: FFT_SIZE },
  (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / FFT_SIZE),
)

export function captureNoisePrint(
  channels: Channels,
  sampleRate: number,
  range: FrameRange,
  source: string,
): NoisePrint {
  const start = Math.max(0, Math.floor(range.start))
  const end = Math.min(channels[0]?.length ?? 0, Math.floor(range.end))
  if (end - start < FFT_SIZE)
    throw new Error(
      `Select at least ${Math.ceil((FFT_SIZE / sampleRate) * 1000)} ms of noise without speech.`,
    )
  const magnitude = new Float32Array(BINS)
  const real = new Float32Array(FFT_SIZE)
  const imaginary = new Float32Array(FFT_SIZE)
  let count = 0
  for (const channel of channels) {
    for (let position = start; position + FFT_SIZE <= end; position += HOP) {
      for (let i = 0; i < FFT_SIZE; i++) real[i] = channel[position + i]! * WINDOW[i]!
      imaginary.fill(0)
      fft(real, imaginary)
      for (let bin = 0; bin < BINS; bin++)
        magnitude[bin]! += Math.hypot(real[bin]!, imaginary[bin]!)
      count++
    }
  }
  for (let bin = 0; bin < BINS; bin++) magnitude[bin]! /= count
  return { sampleRate, magnitude, seconds: (end - start) / sampleRate, source }
}

function reduceChannel(channel: Float32Array, print: NoisePrint, floor: number, strength: number) {
  // Pad a window's worth on both sides so the first and last samples get full overlap.
  const padded = new Float32Array(channel.length + 2 * FFT_SIZE)
  padded.set(channel, FFT_SIZE)
  const accumulated = new Float32Array(padded.length)
  const real = new Float32Array(FFT_SIZE)
  const imaginary = new Float32Array(FFT_SIZE)
  const raw = new Float32Array(BINS)
  const gains = new Float32Array(BINS).fill(1)

  for (let position = 0; position + FFT_SIZE <= padded.length; position += HOP) {
    for (let i = 0; i < FFT_SIZE; i++) real[i] = padded[position + i]! * WINDOW[i]!
    imaginary.fill(0)
    fft(real, imaginary)

    for (let bin = 0; bin < BINS; bin++) {
      const magnitude = Math.hypot(real[bin]!, imaginary[bin]!)
      raw[bin] = Math.max(floor, 1 - (strength * print.magnitude[bin]!) / (magnitude + 1e-12))
    }
    for (let bin = 0; bin < BINS; bin++) {
      // Average with the neighbouring bins, then move towards it at the open or close rate.
      const target =
        (raw[Math.max(0, bin - 1)]! + raw[bin]! + raw[Math.min(BINS - 1, bin + 1)]!) / 3
      const previous = gains[bin]!
      const gain = previous + (target - previous) * (target > previous ? OPEN_RATE : CLOSE_RATE)
      gains[bin] = gain
      real[bin]! *= gain
      imaginary[bin]! *= gain
      if (bin > 0 && bin < FFT_SIZE / 2) {
        real[FFT_SIZE - bin]! *= gain
        imaginary[FFT_SIZE - bin]! *= gain
      }
    }

    // Inverse transform through the forward one: conjugate, transform, conjugate, scale.
    for (let i = 0; i < FFT_SIZE; i++) imaginary[i] = -imaginary[i]!
    fft(real, imaginary)
    for (let i = 0; i < FFT_SIZE; i++)
      accumulated[position + i]! += (real[i]! / FFT_SIZE) * WINDOW[i]!
  }

  const output = new Float32Array(channel.length)
  for (let i = 0; i < output.length; i++) output[i] = accumulated[FFT_SIZE + i]! / OVERLAP_GAIN
  return output
}

export function reduceNoise(
  channels: Channels,
  sampleRate: number,
  print: NoisePrint,
  options: NoiseReductionOptions,
) {
  if (print.sampleRate !== sampleRate)
    throw new Error(
      `The noise print is from a ${print.sampleRate} Hz file; capture one from this ${sampleRate} Hz file.`,
    )
  const floor = dbToGain(-Math.abs(options.reductionDb))
  return channels.map((channel) => reduceChannel(channel, print, floor, options.strength))
}
