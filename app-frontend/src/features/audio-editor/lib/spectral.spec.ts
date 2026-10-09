import { describe, expect, it } from 'vitest'
import {
  extendStroke,
  offsetSelection,
  selectionBounds,
  selectionIntervals,
  spectralEdit,
  subtractIntervals,
  type SpectralEdit,
  type SpectralSelection,
  type SpectralShape,
} from './spectral'

const RATE = 48000

function tone(frequency: number, seconds = 1, amplitude = 0.3) {
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / RATE),
  )
}

function mix(...signals: Float32Array[]) {
  const output = new Float32Array(signals[0]!.length)
  for (const signal of signals) for (let i = 0; i < output.length; i++) output[i]! += signal[i]!
  return output
}

function rms(channel: Float32Array, start = 0, end = channel.length) {
  let sum = 0
  for (let i = start; i < end; i++) sum += channel[i]! ** 2
  return Math.sqrt(sum / (end - start))
}

/** Level of `frequency` in the stretch, Hann-windowed so other tones do not leak in, in dB. */
function levelAt(channel: Float32Array, frequency: number, start: number, end: number) {
  let real = 0
  let imaginary = 0
  const length = end - start
  for (let i = 0; i < length; i++) {
    const weight = 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / length)
    const phase = (2 * Math.PI * frequency * (start + i)) / RATE
    real += channel[start + i]! * weight * Math.cos(phase)
    imaginary += channel[start + i]! * weight * Math.sin(phase)
  }
  return 20 * Math.log10(Math.hypot(real, imaginary) / (length / 4) + 1e-12)
}

function edit(selection: SpectralSelection, patch: Partial<SpectralEdit> = {}): SpectralEdit {
  return {
    selection,
    mode: 'gain',
    gainDb: -40,
    feather: 0,
    direction: 'auto',
    fftSize: 2048,
    ...patch,
  }
}

const band = (
  low: number,
  high: number,
  start = 0.3 * RATE,
  end = 0.7 * RATE,
): SpectralSelection => [{ shape: { kind: 'rect', start, end, low, high }, subtract: false }]

describe('selection geometry', () => {
  it('adds and cuts shapes in order', () => {
    const selection: SpectralSelection = [
      { shape: { kind: 'rect', start: 0, end: 100, low: 100, high: 1000 }, subtract: false },
      { shape: { kind: 'rect', start: 50, end: 100, low: 400, high: 600 }, subtract: true },
    ]
    expect(selectionIntervals(selection, 10)).toEqual([[100, 1000]])
    expect(selectionIntervals(selection, 60)).toEqual([
      [100, 400],
      [600, 1000],
    ])
    expect(selectionIntervals(selection, 100)).toEqual([])
    expect(subtractIntervals([[0, 10]], [[2, 3]])).toEqual([
      [0, 2],
      [3, 10],
    ])
  })

  it('fills a lasso between its crossings', () => {
    const selection: SpectralSelection = [
      {
        shape: {
          kind: 'lasso',
          scale: 'linear',
          points: [
            { frame: 0, frequency: 1000 },
            { frame: 100, frequency: 1000 },
            { frame: 100, frequency: 2000 },
            { frame: 0, frequency: 2000 },
          ],
        },
        subtract: false,
      },
    ]
    expect(selectionIntervals(selection, 50)).toEqual([[1000, 2000]])
    expect(selectionIntervals(selection, 150)).toEqual([])
    expect(selectionBounds(selection)).toEqual({ start: 0, end: 100, low: 1000, high: 2000 })
    expect(selectionBounds(offsetSelection(selection, 10))?.start).toBe(10)
  })

  it('fills the gaps of a fast brush stroke', () => {
    let stroke: Extract<SpectralShape, { kind: 'brush' }> = {
      kind: 'brush',
      scale: 'log',
      points: [],
      radiusFrames: 100,
      radiusWarp: 0.25,
    }
    stroke = extendStroke(stroke, { frame: 0, frequency: 1000 })
    stroke = extendStroke(stroke, { frame: 1000, frequency: 1000 })
    const selection: SpectralSelection = [{ shape: stroke, subtract: false }]
    for (let frame = 0; frame <= 1000; frame += 50)
      expect(selectionIntervals(selection, frame).length).toBe(1)
  })
})

describe('spectralEdit', () => {
  it('leaves audio outside the selection exactly as it was', () => {
    const input = mix(tone(500), tone(3000))
    const [output] = spectralEdit([input], RATE, edit(band(2500, 3500)))
    // Far from the selection in time nothing is touched at all.
    for (let i = 0; i < 0.2 * RATE; i += 97) expect(output![i]).toBe(input[i])
    // Inside its time but below its band, the 500 Hz tone is unchanged.
    const start = 0.4 * RATE
    const end = 0.6 * RATE
    expect(levelAt(output!, 500, start, end)).toBeCloseTo(levelAt(input, 500, start, end), 0)
  })

  it('turns down only the selected band', () => {
    const input = mix(tone(500), tone(3000))
    const [output] = spectralEdit([input], RATE, edit(band(2500, 3500), { gainDb: -30 }))
    const start = 0.4 * RATE
    const end = 0.6 * RATE
    const drop = levelAt(input, 3000, start, end) - levelAt(output!, 3000, start, end)
    expect(drop).toBeGreaterThan(25)
    expect(drop).toBeLessThan(35)
  })

  it('reconstructs perfectly where the mask is empty', () => {
    const input = tone(440, 0.5)
    const empty: SpectralSelection = [
      { shape: { kind: 'rect', start: 1000, end: 2000, low: 100, high: 200 }, subtract: false },
      { shape: { kind: 'rect', start: 0, end: 24000, low: 0, high: 24000 }, subtract: true },
    ]
    const [output] = spectralEdit([input], RATE, edit(empty, { gainDb: -60, feather: 0.5 }))
    let error = 0
    for (let i = 0; i < input.length; i++)
      error = Math.max(error, Math.abs(output![i]! - input[i]!))
    expect(error).toBeLessThan(1e-5)
  })

  it('deletes a band and isolates one', () => {
    const input = mix(tone(500), tone(3000))
    const selection = band(2500, 3500, 0, RATE)
    const [deleted] = spectralEdit([input], RATE, edit(selection, { mode: 'delete' }))
    const [isolated] = spectralEdit([input], RATE, edit(selection, { mode: 'isolate' }))
    const start = 0.3 * RATE
    const end = 0.7 * RATE
    expect(levelAt(deleted!, 3000, start, end)).toBeLessThan(levelAt(input, 3000, start, end) - 60)
    expect(levelAt(isolated!, 500, start, end)).toBeLessThan(levelAt(input, 500, start, end) - 60)
    expect(levelAt(isolated!, 3000, start, end)).toBeCloseTo(levelAt(input, 3000, start, end), 0)
  })

  it('heals a burst back to the level around it', () => {
    const bed = tone(800, 1, 0.2)
    const burst = Float32Array.from(bed, (_, i) =>
      i >= 0.48 * RATE && i < 0.52 * RATE
        ? Math.sin(i * 1.7) * 0.6 * Math.sin((i * 2 * Math.PI) / 6)
        : 0,
    )
    const input = mix(bed, burst)
    const selection = band(0, 24000, 0.47 * RATE, 0.53 * RATE)
    const [output] = spectralEdit([input], RATE, edit(selection, { mode: 'heal', feather: 0.3 }))
    const inside = rms(output!, 0.48 * RATE, 0.52 * RATE)
    const around = rms(bed, 0.48 * RATE, 0.52 * RATE)
    expect(rms(input, 0.48 * RATE, 0.52 * RATE)).toBeGreaterThan(around * 1.8)
    expect(inside).toBeLessThan(around * 1.3)
    // The tone underneath survives.
    expect(inside).toBeGreaterThan(around * 0.6)
  })

  it('refuses to heal more than it can hold', () => {
    const input = new Float32Array(RATE * 40)
    expect(() =>
      spectralEdit([input], RATE, edit(band(100, 200, 0, RATE * 35), { mode: 'heal' })),
    ).toThrow(/30 s/)
  })
})
