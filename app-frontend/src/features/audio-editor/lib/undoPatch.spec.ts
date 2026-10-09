import { describe, expect, it } from 'vitest'
import { pasteInsert } from './editing'
import { deleteRange, gain, toMono, type Channels } from './operations'
import { applyPatch, diffPatch, patchBytes } from './undoPatch'

const stereo = (): Channels => [
  Float32Array.from({ length: 1000 }, (_, i) => Math.sin(i * 0.1)),
  Float32Array.from({ length: 1000 }, (_, i) => Math.cos(i * 0.07)),
]

/** Undoes and redoes the edit through its patch, checking both land exactly. */
function roundTrip(before: Channels, after: Channels) {
  const patch = diffPatch(before, after)!
  const undone = applyPatch(after, patch)
  expect(undone.channels.map((channel) => Array.from(channel))).toEqual(
    before.map((channel) => Array.from(channel)),
  )
  const redone = applyPatch(undone.channels, undone.redo)
  expect(redone.channels.map((channel) => Array.from(channel))).toEqual(
    after.map((channel) => Array.from(channel)),
  )
  return patch
}

describe('undo patches', () => {
  it('keeps only the stretch an edit changed', () => {
    const before = stereo()
    const after = gain(before, -6, { start: 400, end: 450 })
    const patch = roundTrip(before, after)
    expect(patch.start).toBe(400)
    expect(patch.inserted).toBe(50)
    expect(patchBytes(patch)).toBe(50 * 2 * 4)
  })

  it('puts back what a delete cut and takes out what a paste added', () => {
    const before = stereo()
    const deleted = roundTrip(before, deleteRange(before, { start: 100, end: 300 }))
    expect(deleted.inserted).toBe(0)
    expect(deleted.removed[0]).toHaveLength(200)

    const clip = [new Float32Array(30).fill(0.5), new Float32Array(30).fill(-0.5)]
    const pasted = roundTrip(before, pasteInsert(before, clip, 500))
    expect(pasted.removed[0]).toHaveLength(0)
    expect(pasted.inserted).toBe(30)
  })

  it('keeps the arrays themselves when the whole file changed', () => {
    const before = stereo()
    const patch = roundTrip(before, gain(before, 3))
    expect(patch.removed).toBe(before)
    const mono = roundTrip(before, toMono(before))
    expect(mono.removed).toBe(before)
  })

  it('is null when nothing changed', () => {
    const before = stereo()
    expect(diffPatch(before, before)).toBeNull()
    expect(
      diffPatch(
        before,
        before.map((channel) => channel.slice()),
      ),
    ).toBeNull()
  })

  it('handles edits that touch the very start or end', () => {
    const before = stereo()
    roundTrip(before, gain(before, -3, { start: 0, end: 10 }))
    roundTrip(before, gain(before, -3, { start: 990, end: 1000 }))
    roundTrip(before, deleteRange(before, { start: 0, end: 10 }))
    roundTrip(before, deleteRange(before, { start: 990, end: 1000 }))
  })
})
