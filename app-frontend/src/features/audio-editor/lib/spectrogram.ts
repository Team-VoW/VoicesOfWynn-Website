import { fft } from '@/components/audio/fft'
import type { Channels } from './operations'

export interface Spectrogram {
  columns: number
  rows: number
  /** Frames between the centres of neighbouring columns. */
  hop: number
  /** Row-major intensity 0-255, row 0 being the highest frequency. */
  intensity: Uint8Array
}

const FFT_SIZE = 2048
const ROWS = 256
const MAX_COLUMNS = 4096
const MIN_FREQUENCY = 60

/**
 * Log-frequency magnitude image of the channel mix, at most MAX_COLUMNS wide. The view draws the
 * visible slice of it stretched to the canvas.
 */
export function buildSpectrogram(channels: Channels, sampleRate: number): Spectrogram {
  const frames = channels[0]?.length ?? 0
  const hop = Math.max(64, Math.ceil(frames / MAX_COLUMNS))
  const columns = Math.max(1, Math.ceil(frames / hop))
  const intensity = new Uint8Array(columns * ROWS)
  const real = new Float32Array(FFT_SIZE)
  const imaginary = new Float32Array(FFT_SIZE)
  const window = Float32Array.from(
    { length: FFT_SIZE },
    (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (FFT_SIZE - 1)),
  )
  const maxFrequency = sampleRate / 2
  const bins = Float32Array.from({ length: ROWS }, (_, row) => {
    const frequency = MIN_FREQUENCY * Math.pow(maxFrequency / MIN_FREQUENCY, 1 - row / (ROWS - 1))
    return Math.min(FFT_SIZE / 2 - 2, Math.max(1, (frequency * FFT_SIZE) / sampleRate))
  })
  const scale = channels.length || 1

  for (let column = 0; column < columns; column++) {
    const center = column * hop + hop / 2
    for (let i = 0; i < FFT_SIZE; i++) {
      const index = Math.floor(center + i - FFT_SIZE / 2)
      let sample = 0
      for (const channel of channels) sample += channel[index] ?? 0
      real[i] = (sample / scale) * window[i]!
      imaginary[i] = 0
    }
    fft(real, imaginary)
    for (let row = 0; row < ROWS; row++) {
      const exact = bins[row]!
      const bin = Math.floor(exact)
      const blend = exact - bin
      const low = Math.hypot(real[bin]!, imaginary[bin]!)
      const high = Math.hypot(real[bin + 1]!, imaginary[bin + 1]!)
      const magnitude = (low * (1 - blend) + high * blend) / (FFT_SIZE * 0.25)
      const decibels = 20 * Math.log10(magnitude + 1e-9)
      const level = Math.pow(Math.min(1, Math.max(0, (decibels + 96) / 84)), 1.3)
      intensity[row * columns + column] = Math.round(level * 255)
    }
  }
  return { columns, rows: ROWS, hop, intensity }
}
