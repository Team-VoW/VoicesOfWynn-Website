import { fft } from '@/components/audio/fft'
import { axisFrequency, type FrequencyAxis } from './frequencyScale'
import type { Channels } from './operations'

export const FFT_SIZES = [256, 512, 1024, 2048, 4096, 8192] as const
/** The loudest level drawn at full brightness; a full-scale sine reads 0 dB. */
const TOP_DB = -12
/** Brightness rises a little faster than level, so quiet detail stays visible. */
const GAMMA = 1.3

/** One image of `columns` × `rows`, column `c` centred on frame `start + (c + 0.5) * hop`. */
export interface SpectrogramRequest {
  start: number
  /** Frames between the centres of neighbouring columns; below 1 when zoomed in past samples. */
  hop: number
  columns: number
  rows: number
  fftSize: number
  /** The frequencies the rows span, top to bottom. */
  axis: FrequencyAxis
  /** Levels this far below the top are drawn black. */
  rangeDb: number
}

export interface Spectrogram extends SpectrogramRequest {
  /** Row-major intensity 0-255, row 0 being the highest frequency. */
  intensity: Uint8Array
}

/** The level an intensity on the image stands for, in dB. */
export function intensityDb(intensity: number, rangeDb: number) {
  return TOP_DB - rangeDb + Math.pow(intensity / 255, 1 / GAMMA) * rangeDb
}

/**
 * The mono samples the FFTs of a request read. Built on the page so the worker gets a few MB for
 * the view on screen rather than a copy of a 2-hour file: one window per column when the columns
 * are far apart, else the stretch they cover, starting half a window before `start`.
 */
export interface SpectrogramInput {
  samples: Float32Array
  gathered: boolean
}

function columnStart(request: SpectrogramRequest, column: number) {
  return Math.floor(request.start + (column + 0.5) * request.hop) - request.fftSize / 2
}

export function spectrogramInput(
  channels: Channels,
  request: SpectrogramRequest,
): SpectrogramInput {
  const { fftSize } = request
  const gathered = request.hop > fftSize
  const first = columnStart(request, 0)
  const length = gathered
    ? request.columns * fftSize
    : columnStart(request, request.columns - 1) - first + fftSize
  const samples = new Float32Array(Math.max(0, length))
  const scale = 1 / (channels.length || 1)
  const frames = channels[0]?.length ?? 0
  const copy = (from: number, to: number, at: number) => {
    const start = Math.max(0, from)
    const end = Math.min(frames, to)
    for (const channel of channels)
      for (let i = start; i < end; i++) samples[at + i - from]! += channel[i]! * scale
  }
  if (gathered)
    for (let column = 0; column < request.columns; column++) {
      const from = columnStart(request, column)
      copy(from, from + fftSize, column * fftSize)
    }
  else copy(first, first + length, 0)
  return { samples, gathered }
}

/**
 * Magnitude image of the input over the request's frequency axis. A row narrower than a bin
 * interpolates between the two nearest; a row wider than one (high up on a log scale, or zoomed
 * out on a linear one) takes the loudest bin it covers, so a thin whistle never falls between rows.
 */
export function buildSpectrogram(
  input: SpectrogramInput,
  sampleRate: number,
  request: SpectrogramRequest,
): Spectrogram {
  const { columns, rows, fftSize, axis, rangeDb } = request
  const intensity = new Uint8Array(columns * rows)
  const real = new Float32Array(fftSize)
  const imaginary = new Float32Array(fftSize)
  const magnitudes = new Float32Array(fftSize / 2 + 1)
  const window = Float32Array.from(
    { length: fftSize },
    (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (fftSize - 1)),
  )
  const lastBin = fftSize / 2
  const binOf = (position: number) =>
    Math.min(lastBin, Math.max(0, (axisFrequency(axis, position) * fftSize) / sampleRate))
  const span = Math.max(1, rows - 1)
  // Each row's centre bin, and the bins its band covers from halfway to the row above to below.
  const centres = Float32Array.from({ length: rows }, (_, row) => binOf(row / span))
  const highs = Int32Array.from({ length: rows }, (_, row) => Math.floor(binOf((row - 0.5) / span)))
  const lows = Int32Array.from({ length: rows }, (_, row) => Math.ceil(binOf((row + 0.5) / span)))
  const first = columnStart(request, 0)
  const { samples } = input
  const normal = fftSize * 0.25

  for (let column = 0; column < columns; column++) {
    const offset = input.gathered ? column * fftSize : columnStart(request, column) - first
    for (let i = 0; i < fftSize; i++) {
      real[i] = (samples[offset + i] ?? 0) * window[i]!
      imaginary[i] = 0
    }
    fft(real, imaginary)
    for (let bin = 0; bin <= lastBin; bin++)
      magnitudes[bin] = Math.hypot(real[bin]!, imaginary[bin]!) / normal
    for (let row = 0; row < rows; row++) {
      let magnitude: number
      const low = lows[row]!
      const high = highs[row]!
      if (high - low >= 1) {
        magnitude = 0
        for (let bin = low; bin <= high; bin++) magnitude = Math.max(magnitude, magnitudes[bin]!)
      } else {
        const exact = centres[row]!
        const bin = Math.min(lastBin - 1, Math.floor(exact))
        const blend = exact - bin
        magnitude = magnitudes[bin]! * (1 - blend) + magnitudes[bin + 1]! * blend
      }
      const decibels = 20 * Math.log10(magnitude + 1e-9)
      const level = Math.pow(
        Math.min(1, Math.max(0, (decibels - TOP_DB + rangeDb) / rangeDb)),
        GAMMA,
      )
      intensity[row * columns + column] = Math.round(level * 255)
    }
  }
  return { ...request, intensity }
}
