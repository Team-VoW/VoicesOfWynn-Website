import { fft } from '@/components/audio/fft'
import type { Channels, FrameRange } from './operations'

export interface AverageSpectrum {
  /** Log-spaced from MIN_FREQUENCY to Nyquist (or 20 kHz). */
  frequencies: Float32Array
  /** Average level of the channel mix at each frequency, in dBFS. */
  levelsDb: Float32Array
}

const FFT_SIZE = 4096
const HOP = FFT_SIZE / 2
/** A long file is sampled at evenly spaced windows instead of read end to end. */
const MAX_WINDOWS = 300
export const MIN_FREQUENCY = 20
export const MAX_FREQUENCY = 20000
/** Each point averages the bins within a sixth of an octave of it, like an analyzer's smoothing. */
const SMOOTHING_OCTAVES = 1 / 6

/** The long-term spectrum of the file (or `range`), for drawing under an EQ curve. */
export function averageSpectrum(
  channels: Channels,
  sampleRate: number,
  range: FrameRange | null = null,
  points = 160,
): AverageSpectrum {
  const start = range?.start ?? 0
  const end = range?.end ?? channels[0]?.length ?? 0
  const available = Math.max(1, Math.floor((end - start - FFT_SIZE) / HOP) + 1)
  const windows = Math.min(MAX_WINDOWS, available)
  const stride = available / windows

  const window = Float32Array.from(
    { length: FFT_SIZE },
    (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / FFT_SIZE),
  )
  const power = new Float64Array(FFT_SIZE / 2 + 1)
  const real = new Float32Array(FFT_SIZE)
  const imaginary = new Float32Array(FFT_SIZE)
  const scale = channels.length || 1
  for (let w = 0; w < windows; w++) {
    const offset = start + Math.floor(w * stride) * HOP
    for (let i = 0; i < FFT_SIZE; i++) {
      const index = offset + i
      let sample = 0
      if (index < end) for (const channel of channels) sample += channel[index] ?? 0
      real[i] = (sample / scale) * window[i]!
      imaginary[i] = 0
    }
    fft(real, imaginary)
    for (let bin = 0; bin < power.length; bin++)
      power[bin]! += real[bin]! ** 2 + imaginary[bin]! ** 2
  }

  // A full-scale sine through a Hann window peaks at FFT_SIZE / 4 in one bin.
  const reference = (FFT_SIZE / 4) ** 2 * windows
  const top = Math.min(MAX_FREQUENCY, sampleRate / 2)
  const binWidth = sampleRate / FFT_SIZE
  const frequencies = Float32Array.from(
    { length: points },
    (_, i) => MIN_FREQUENCY * Math.pow(top / MIN_FREQUENCY, i / (points - 1)),
  )
  const levelsDb = frequencies.map((frequency) => {
    const spread = Math.pow(2, SMOOTHING_OCTAVES / 2)
    let low = Math.max(1, Math.ceil(frequency / spread / binWidth))
    let high = Math.min(power.length - 1, Math.floor((frequency * spread) / binWidth))
    if (high < low) low = high = Math.min(power.length - 1, Math.round(frequency / binWidth))
    let sum = 0
    for (let bin = low; bin <= high; bin++) sum += power[bin]!
    return 10 * Math.log10(sum / (high - low + 1) / reference + 1e-12)
  })
  return { frequencies, levelsDb }
}
