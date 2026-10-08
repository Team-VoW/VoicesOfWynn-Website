// Min/max waveform summaries at several resolutions, like map tiles: zoomed out the waveform view
// reads a coarse level instead of scanning every sample on each frame.

import type { Channels } from './operations'

export interface PeakLevel {
  samplesPerBucket: number
  /** Per channel: interleaved [min0, max0, min1, max1, …]. */
  channels: Float32Array[]
}

export type PeakPyramid = PeakLevel[]

const BASE_BUCKET = 64
const LEVEL_FACTOR = 4
const LEVELS = 4 // 64, 256, 1024, 4096

export function buildPeaks(channels: Channels): PeakPyramid {
  const levels: PeakPyramid = []
  const base = channels.map((samples) => {
    const buckets = Math.ceil(samples.length / BASE_BUCKET)
    const output = new Float32Array(buckets * 2)
    for (let bucket = 0; bucket < buckets; bucket++) {
      let min = Infinity,
        max = -Infinity
      const end = Math.min(samples.length, (bucket + 1) * BASE_BUCKET)
      for (let i = bucket * BASE_BUCKET; i < end; i++) {
        const value = samples[i]!
        if (value < min) min = value
        if (value > max) max = value
      }
      output[bucket * 2] = min
      output[bucket * 2 + 1] = max
    }
    return output
  })
  levels.push({ samplesPerBucket: BASE_BUCKET, channels: base })

  for (let level = 1; level < LEVELS; level++) {
    const previous = levels[level - 1]!
    levels.push({
      samplesPerBucket: previous.samplesPerBucket * LEVEL_FACTOR,
      channels: previous.channels.map((source) => {
        const sourceBuckets = source.length / 2
        const buckets = Math.ceil(sourceBuckets / LEVEL_FACTOR)
        const output = new Float32Array(buckets * 2)
        for (let bucket = 0; bucket < buckets; bucket++) {
          let min = Infinity,
            max = -Infinity
          const end = Math.min(sourceBuckets, (bucket + 1) * LEVEL_FACTOR)
          for (let i = bucket * LEVEL_FACTOR; i < end; i++) {
            min = Math.min(min, source[i * 2]!)
            max = Math.max(max, source[i * 2 + 1]!)
          }
          output[bucket * 2] = min
          output[bucket * 2 + 1] = max
        }
        return output
      }),
    })
  }
  return levels
}

/** The coarsest level that still has at least one bucket per pixel. */
export function pickLevel(pyramid: PeakPyramid, samplesPerPixel: number) {
  let chosen: PeakLevel | null = null
  for (const level of pyramid) if (level.samplesPerBucket <= samplesPerPixel) chosen = level
  return chosen
}
