// Audition-style clipboard edits and zero-crossing adjustments. Like ./operations, nothing here
// mutates its input.

import { dbToGain, frameCount, type Channels, type FrameRange } from './operations'
import { resample } from './resample'

export interface ClipboardAudio {
  channels: Channels
  sampleRate: number
  /** File the audio was copied from, for display. */
  source: string
}

export type ZeroCrossingAdjustment =
  | 'inward'
  | 'outward'
  | 'startLeft'
  | 'startRight'
  | 'endLeft'
  | 'endRight'

export interface MixPasteOptions {
  /** Overlap mixes the clip into what is there; overwrite replaces it. */
  mode: 'overlap' | 'overwrite'
  volumeDb: number
  /** Fades the clip in and out over this many frames at both of its edges. */
  crossfadeFrames: number
}

function summed(channels: Channels, frame: number) {
  let value = 0
  for (const channel of channels) value += channel[frame]!
  return value
}

/** True when the channel mix changes sign between `frame - 1` and `frame`. */
function crossesAt(channels: Channels, frame: number) {
  const before = summed(channels, frame - 1)
  const after = summed(channels, frame)
  return before === 0 || after === 0 || before < 0 !== after < 0
}

/**
 * The closest frame boundary at or beside `frame` where the channel mix crosses zero, so a cut
 * there joins two near-silent samples instead of clicking. The file edges always count. Returns
 * `frame` unchanged when nothing lies within `maxDistance` frames.
 */
export function zeroCrossing(
  channels: Channels,
  frame: number,
  direction: 'left' | 'right' | 'nearest',
  maxDistance: number,
) {
  const frames = frameCount(channels)
  const start = Math.max(0, Math.min(frames, Math.round(frame)))
  for (let distance = 0; distance <= maxDistance; distance++) {
    for (const side of direction === 'nearest' ? [-1, 1] : [direction === 'left' ? -1 : 1]) {
      const candidate = start + side * distance
      if (candidate <= 0) return 0
      if (candidate >= frames) return frames
      if (crossesAt(channels, candidate)) return candidate
    }
  }
  return start
}

/** Edit > Zero Crossings in Audition. Returns null when inward would leave nothing selected. */
export function adjustToZeroCrossings(
  channels: Channels,
  selection: FrameRange,
  adjustment: ZeroCrossingAdjustment,
  maxDistance: number,
): FrameRange | null {
  const move = (frame: number, direction: 'left' | 'right') =>
    zeroCrossing(channels, frame, direction, maxDistance)
  let { start, end } = selection
  switch (adjustment) {
    case 'inward':
      start = move(start, 'right')
      end = move(end, 'left')
      break
    case 'outward':
      start = move(start, 'left')
      end = move(end, 'right')
      break
    case 'startLeft':
      start = move(start - 1, 'left')
      break
    case 'startRight':
      start = move(start + 1, 'right')
      break
    case 'endLeft':
      end = move(end - 1, 'left')
      break
    case 'endRight':
      end = move(end + 1, 'right')
      break
  }
  return end > start ? { start, end } : null
}

/** The clipboard at the target's sample rate and channel count. */
export function conformClip(clip: ClipboardAudio, channelCount: number, sampleRate: number) {
  const source =
    clip.sampleRate === sampleRate
      ? clip.channels
      : clip.channels.map((channel) => resample(channel, clip.sampleRate, sampleRate))
  if (source.length === channelCount) return source
  if (channelCount === 1) {
    const mono = new Float32Array(frameCount(source))
    for (const channel of source)
      for (let i = 0; i < mono.length; i++) mono[i]! += channel[i]! / source.length
    return [mono]
  }
  return Array.from({ length: channelCount }, (_, index) => source[index % source.length]!)
}

/** Paste: inserts the clip at `at`, or in place of `replace` when there is a selection. */
export function pasteInsert(
  channels: Channels,
  clip: Channels,
  at: number,
  replace?: FrameRange | null,
) {
  const frames = frameCount(channels)
  const start = Math.max(0, Math.min(frames, Math.round(replace?.start ?? at)))
  const end = replace ? Math.max(start, Math.min(frames, Math.round(replace.end))) : start
  const length = frameCount(clip)
  return channels.map((source, index) => {
    const output = new Float32Array(frames - (end - start) + length)
    output.set(source.subarray(0, start), 0)
    output.set(clip[index]!, start)
    output.set(source.subarray(end), start + length)
    return output
  })
}

/** Mix Paste: lays the clip over the audio from `at`, extending the file if it runs past the end. */
export function mixPaste(channels: Channels, clip: Channels, at: number, options: MixPasteOptions) {
  const frames = frameCount(channels)
  const start = Math.max(0, Math.min(frames, Math.round(at)))
  const length = frameCount(clip)
  const volume = dbToGain(options.volumeDb)
  const fade = Math.max(0, Math.min(Math.floor(length / 2), Math.round(options.crossfadeFrames)))
  const envelope = (offset: number) =>
    fade === 0 ? 1 : Math.min(1, (offset + 0.5) / fade, (length - offset - 0.5) / fade)
  return channels.map((source, index) => {
    const output = new Float32Array(Math.max(frames, start + length))
    output.set(source)
    const pasted = clip[index]!
    for (let offset = 0; offset < length; offset++) {
      const frame = start + offset
      const weight = envelope(offset)
      const existing = output[frame]!
      output[frame] =
        options.mode === 'overlap'
          ? existing + pasted[offset]! * volume * weight
          : existing * (1 - weight) + pasted[offset]! * volume * weight
    }
    return output
  })
}
