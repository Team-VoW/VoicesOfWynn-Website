import { describe, expect, it } from 'vitest'
import { adjustToZeroCrossings, conformClip, mixPaste, pasteInsert, zeroCrossing } from './editing'
import { blendRange } from './operations'
import { resample } from './resample'

const values = (array: Float32Array) => Array.from(array)
// Crosses zero between frames 2→3 and 6→7.
const wave = () => [Float32Array.from([0.5, 0.4, 0.2, -0.1, -0.3, -0.2, -0.1, 0.2, 0.4])]

describe('zero crossings', () => {
  it('finds the nearest crossing either side', () => {
    expect(zeroCrossing(wave(), 2, 'nearest', 10)).toBe(3)
    expect(zeroCrossing(wave(), 6, 'nearest', 10)).toBe(7)
  })

  it('searches in one direction', () => {
    expect(zeroCrossing(wave(), 5, 'left', 10)).toBe(3)
    expect(zeroCrossing(wave(), 5, 'right', 10)).toBe(7)
  })

  it('stays put when nothing is in reach', () => {
    expect(zeroCrossing(wave(), 5, 'nearest', 1)).toBe(5)
  })

  it('adjusts a selection inward and outward', () => {
    expect(adjustToZeroCrossings(wave(), { start: 2, end: 8 }, 'inward', 10)).toEqual({
      start: 3,
      end: 7,
    })
    expect(adjustToZeroCrossings(wave(), { start: 4, end: 6 }, 'outward', 10)).toEqual({
      start: 3,
      end: 7,
    })
  })

  it('returns null when inward leaves nothing', () => {
    expect(adjustToZeroCrossings(wave(), { start: 4, end: 6 }, 'inward', 10)).toBeNull()
  })
})

describe('paste', () => {
  const base = () => [Float32Array.from([1, 2, 3, 4])]
  const clip = [Float32Array.from([9, 9])]

  it('inserts at the cursor', () => {
    expect(values(pasteInsert(base(), clip, 1)[0]!)).toEqual([1, 9, 9, 2, 3, 4])
  })

  it('replaces the selection', () => {
    expect(values(pasteInsert(base(), clip, 0, { start: 1, end: 3 })[0]!)).toEqual([1, 9, 9, 4])
  })

  it('mixes over the existing audio and extends past the end', () => {
    const result = mixPaste(base(), clip, 3, { mode: 'overlap', volumeDb: 0, crossfadeFrames: 0 })
    expect(values(result[0]!)).toEqual([1, 2, 3, 13, 9])
  })

  it('overwrites with a crossfade at both edges', () => {
    const silent = [new Float32Array(6)]
    const ones = [new Float32Array(6).fill(1)]
    const result = mixPaste(silent, ones, 0, { mode: 'overwrite', volumeDb: 0, crossfadeFrames: 2 })
    expect(values(result[0]!)).toEqual([0.25, 0.75, 1, 1, 0.75, 0.25])
  })
})

describe('conformClip', () => {
  it('mixes stereo down to mono and duplicates mono to stereo', () => {
    const stereo = {
      channels: [Float32Array.from([1]), Float32Array.from([0])],
      sampleRate: 1,
      source: '',
    }
    expect(values(conformClip(stereo, 1, 1)[0]!)).toEqual([0.5])
    const mono = { channels: [Float32Array.from([0.25])], sampleRate: 1, source: '' }
    expect(conformClip(mono, 2, 1).map(values)).toEqual([[0.25], [0.25]])
  })

  it('resamples to the target rate', () => {
    const clip = { channels: [new Float32Array(441)], sampleRate: 44100, source: '' }
    expect(conformClip(clip, 1, 48000)[0]).toHaveLength(480)
  })
})

describe('resample', () => {
  it('keeps a tone well below Nyquist intact', () => {
    const from = 44100
    const to = 48000
    const tone = Float32Array.from({ length: 4410 }, (_, i) =>
      Math.sin((2 * Math.PI * 1000 * i) / from),
    )
    const output = resample(tone, from, to)
    let error = 0
    for (let i = 200; i < output.length - 200; i++)
      error = Math.max(error, Math.abs(output[i]! - Math.sin((2 * Math.PI * 1000 * i) / to)))
    expect(error).toBeLessThan(0.01)
  })
})

describe('blendRange', () => {
  it('only changes the range, ramping in and out', () => {
    const dry = [new Float32Array(8)]
    const wet = [new Float32Array(8).fill(1)]
    expect(values(blendRange(dry, wet, { start: 2, end: 6 }, 2)[0]!)).toEqual([
      0, 0, 0.25, 0.75, 0.75, 0.25, 0, 0,
    ])
  })
})
