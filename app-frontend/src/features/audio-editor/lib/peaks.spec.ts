import { describe, expect, it } from 'vitest'
import { buildPeaks, peakInRange } from './peaks'

describe('peakInRange', () => {
  it('finds the loudest sample of a range, negative ones included, across channels', () => {
    const left = new Float32Array(10000)
    const right = new Float32Array(10000)
    left[5000] = 0.5
    right[7000] = -0.8
    const peaks = buildPeaks([left, right])

    expect(peakInRange(peaks, 0, 10000)).toBeCloseTo(0.8)
    expect(peakInRange(peaks, 4000, 6000)).toBeCloseTo(0.5)
    expect(peakInRange(peaks, 0, 1000)).toBe(0)
  })
})
