// Undo steps that keep only the audio an edit changed. Healing half a second of a two-hour take
// holds on to half a second, not a second copy of the take, so long files keep their full history.

import { frameCount, type Channels } from './operations'

/**
 * How to get the audio before an edit back from the audio after it: put `removed` back in place
 * of the `inserted` frames from `start`. One shape covers every edit: one that keeps the length
 * replaces a stretch with one as long, a delete takes nothing out and puts the cut back, a paste
 * takes the pasted frames out and puts nothing back.
 */
export interface AudioPatch {
  start: number
  removed: Channels
  inserted: number
}

function sameFrame(a: Channels, aFrame: number, b: Channels, bFrame: number) {
  for (let channel = 0; channel < a.length; channel++)
    if (a[channel]![aFrame] !== b[channel]![bFrame]) return false
  return true
}

/**
 * The patch that turns `after` back into `before`: everything between the frames both start with
 * and the frames both end with. Null when the edit left the audio exactly as it was.
 */
export function diffPatch(before: Channels, after: Channels): AudioPatch | null {
  if (before === after) return null
  const beforeFrames = frameCount(before)
  const afterFrames = frameCount(after)
  // Another channel count (to mono, say) shares nothing frame by frame.
  if (before.length !== after.length) return { start: 0, removed: before, inserted: afterFrames }
  const shortest = Math.min(beforeFrames, afterFrames)
  let start = 0
  while (start < shortest && sameFrame(before, start, after, start)) start++
  if (start === beforeFrames && start === afterFrames) return null
  let end = 0
  while (
    end < shortest - start &&
    sameFrame(before, beforeFrames - 1 - end, after, afterFrames - 1 - end)
  )
    end++
  const inserted = afterFrames - start - end
  // The whole file changed: keep the arrays themselves rather than a copy of them.
  if (start === 0 && end === 0) return { start: 0, removed: before, inserted }
  return {
    start,
    removed: before.map((channel) => channel.slice(start, beforeFrames - end)),
    inserted,
  }
}

/** Applies the patch to `after`, returning the audio before the edit and the patch that redoes it. */
export function applyPatch(
  after: Channels,
  patch: AudioPatch,
): { channels: Channels; redo: AudioPatch } {
  const frames = frameCount(after)
  const { start, removed, inserted } = patch
  const removedFrames = frameCount(removed)
  if (start === 0 && inserted === frames)
    return { channels: removed, redo: { start: 0, removed: after, inserted: removedFrames } }
  const channels = after.map((channel, index) => {
    const output = new Float32Array(frames - inserted + removedFrames)
    output.set(channel.subarray(0, start), 0)
    output.set(removed[index]!, start)
    output.set(channel.subarray(start + inserted), start + removedFrames)
    return output
  })
  return {
    channels,
    redo: {
      start,
      removed: after.map((channel) => channel.slice(start, start + inserted)),
      inserted: removedFrames,
    },
  }
}

/** Memory a patch holds on to. */
export function patchBytes(patch: AudioPatch | null) {
  return patch?.removed.reduce((sum, channel) => sum + channel.byteLength, 0) ?? 0
}
