// Destructive edits on planar audio. Every operation returns new arrays and never mutates its
// input, so the editor can keep the previous buffers as an undo snapshot.

export type Channels = Float32Array[]

/** Half-open frame range [start, end). */
export interface FrameRange {
  start: number
  end: number
}

export type FadeCurve = 'linear' | 'logarithmic'

export function frameCount(channels: Channels) {
  return channels[0]?.length ?? 0
}

export function dbToGain(db: number) {
  return Math.pow(10, db / 20)
}

export function gainToDb(gain: number) {
  return gain > 0 ? 20 * Math.log10(gain) : -Infinity
}

function clampRange(channels: Channels, range?: FrameRange | null): FrameRange {
  const frames = frameCount(channels)
  if (!range) return { start: 0, end: frames }
  const start = Math.max(0, Math.min(frames, Math.floor(range.start)))
  const end = Math.max(start, Math.min(frames, Math.floor(range.end)))
  return { start, end }
}

function mapRange(
  channels: Channels,
  range: FrameRange | null | undefined,
  transform: (sample: number, frame: number, start: number, end: number) => number,
) {
  const { start, end } = clampRange(channels, range)
  return channels.map((source) => {
    const output = source.slice()
    for (let frame = start; frame < end; frame++)
      output[frame] = transform(source[frame]!, frame, start, end)
    return output
  })
}

/**
 * `processed` inside the range and `original` outside it, crossfading over `fadeFrames` at both
 * edges inside the range so an effect applied to a selection does not click where it starts and
 * stops. The whole of `processed` when there is no range.
 */
export function blendRange(
  original: Channels,
  processed: Channels,
  range: FrameRange | null | undefined,
  fadeFrames: number,
) {
  if (!range) return processed
  const { start, end } = clampRange(original, range)
  const fade = Math.max(0, Math.min(Math.floor((end - start) / 2), Math.round(fadeFrames)))
  return original.map((source, index) => {
    const output = source.slice()
    const wet = processed[index]!
    for (let frame = start; frame < end; frame++) {
      const weight =
        fade === 0 ? 1 : Math.min(1, (frame - start + 0.5) / fade, (end - frame - 0.5) / fade)
      output[frame] = source[frame]! + (wet[frame]! - source[frame]!) * weight
    }
    return output
  })
}

export function deleteRange(channels: Channels, range: FrameRange) {
  const { start, end } = clampRange(channels, range)
  return channels.map((source) => {
    const output = new Float32Array(source.length - (end - start))
    output.set(source.subarray(0, start), 0)
    output.set(source.subarray(end), start)
    return output
  })
}

export function crop(channels: Channels, range: FrameRange) {
  const { start, end } = clampRange(channels, range)
  return channels.map((source) => source.slice(start, end))
}

export function silenceRange(channels: Channels, range: FrameRange) {
  return mapRange(channels, range, () => 0)
}

export function insertSilence(channels: Channels, at: number, frames: number) {
  const position = Math.max(0, Math.min(frameCount(channels), Math.floor(at)))
  const length = Math.max(0, Math.floor(frames))
  return channels.map((source) => {
    const output = new Float32Array(source.length + length)
    output.set(source.subarray(0, position), 0)
    output.set(source.subarray(position), position + length)
    return output
  })
}

export function toMono(channels: Channels): Channels {
  if (channels.length <= 1) return channels.map((channel) => channel.slice())
  const frames = frameCount(channels)
  const output = new Float32Array(frames)
  for (const channel of channels) for (let i = 0; i < frames; i++) output[i]! += channel[i]!
  for (let i = 0; i < frames; i++) output[i]! /= channels.length
  return [output]
}

export function gain(channels: Channels, db: number, range?: FrameRange | null) {
  const factor = dbToGain(db)
  return mapRange(channels, range, (sample) => sample * factor)
}

export function fade(
  channels: Channels,
  range: FrameRange,
  direction: 'in' | 'out',
  curve: FadeCurve = 'linear',
) {
  return mapRange(channels, range, (sample, frame, start, end) => {
    const length = end - start
    const progress = length <= 1 ? 1 : (frame - start) / (length - 1)
    const t = direction === 'in' ? progress : 1 - progress
    return sample * (curve === 'linear' ? t : t * t)
  })
}

export function samplePeak(channels: Channels, range?: FrameRange | null) {
  const { start, end } = clampRange(channels, range)
  let peak = 0
  for (const channel of channels)
    for (let i = start; i < end; i++) peak = Math.max(peak, Math.abs(channel[i]!))
  return peak
}

export function normalizePeak(channels: Channels, targetDb: number, range?: FrameRange | null) {
  const peak = samplePeak(channels, range)
  if (peak === 0) return channels.map((channel) => channel.slice())
  return gain(channels, targetDb - gainToDb(peak), range)
}

/**
 * First and one-past-last frame where any channel rises above the threshold, sample by sample.
 * This is how the Audio check (ffmpeg silencedetect) measures; trimming uses ./silence instead.
 */
export function soundBounds(channels: Channels, thresholdDb: number) {
  const threshold = dbToGain(thresholdDb)
  const frames = frameCount(channels)
  const loud = (frame: number) => channels.some((channel) => Math.abs(channel[frame]!) > threshold)
  let first = 0
  while (first < frames && !loud(first)) first++
  if (first === frames) return null
  let last = frames - 1
  while (last > first && !loud(last)) last--
  return { start: first, end: last + 1 }
}
