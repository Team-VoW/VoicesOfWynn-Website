import { describe, expect, it } from 'vitest'
import {
  crop,
  deleteRange,
  fade,
  gain,
  insertSilence,
  normalizePeak,
  samplePeak,
  toMono,
} from './operations'

const ramp = () => [Float32Array.from([1, 2, 3, 4, 5, 6].map((v) => v / 10))]

describe('operations', () => {
  it('deletes a range and closes the gap', () => {
    expect(Array.from(deleteRange(ramp(), { start: 1, end: 3 })[0]!)).toEqual(
      [0.1, 0.4, 0.5, 0.6].map(Math.fround),
    )
  })

  it('crops to a range', () => {
    expect(Array.from(crop(ramp(), { start: 2, end: 4 })[0]!)).toEqual([0.3, 0.4].map(Math.fround))
  })

  it('inserts silence at a position', () => {
    const result = insertSilence(ramp(), 2, 3)[0]!

    expect(result).toHaveLength(9)
    expect(Array.from(result.subarray(2, 5))).toEqual([0, 0, 0])
    expect(result[5]).toBeCloseTo(0.3)
  })

  it('never mutates its input', () => {
    const input = ramp()
    const before = input[0]!.slice()

    gain(input, 6)
    fade(input, { start: 0, end: 6 }, 'in')

    expect(input[0]).toEqual(before)
  })

  it('applies gain only inside the range', () => {
    const result = gain(ramp(), 20, { start: 0, end: 1 })[0]!

    expect(result[0]).toBeCloseTo(1)
    expect(result[1]).toBeCloseTo(0.2)
  })

  it('fades in from zero to full level', () => {
    const flat = [new Float32Array(5).fill(1)]

    expect(Array.from(fade(flat, { start: 0, end: 5 }, 'in')[0]!)).toEqual([0, 0.25, 0.5, 0.75, 1])
    expect(fade(flat, { start: 0, end: 5 }, 'out', 'logarithmic')[0]![1]).toBeCloseTo(0.5625)
  })

  it('normalizes the peak to the target', () => {
    expect(samplePeak(normalizePeak(ramp(), -6))).toBeCloseTo(0.501, 3)
  })

  it('averages channels into mono', () => {
    const mono = toMono([Float32Array.from([1, 0]), Float32Array.from([0, 1])])

    expect(mono).toHaveLength(1)
    expect(Array.from(mono[0]!)).toEqual([0.5, 0.5])
  })
})
