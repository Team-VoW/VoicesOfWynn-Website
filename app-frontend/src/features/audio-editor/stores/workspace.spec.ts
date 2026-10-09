import { describe, expect, it } from 'vitest'
import { deleteRange, gain, type Channels } from '../lib/operations'
import type { SpectralSelection } from '../lib/spectral'
import { patchBytes } from '../lib/undoPatch'
import { defaultLayout, encodeWav } from '../lib/wav'
import { useAudioWorkspace } from './workspace'

const RATE = 48000

function copy(channels: Channels) {
  return channels.map((channel) => Array.from(channel))
}

describe('undo history', () => {
  it('undoes and redoes a chain of edits exactly, keeping only what each changed', async () => {
    const workspace = useAudioWorkspace()
    const tone = Float32Array.from({ length: RATE * 20 }, (_, i) => 0.3 * Math.sin(i * 0.05))
    const bytes = encodeWav([tone], RATE, defaultLayout(1, 'float'))
    await workspace.addFiles([
      { file: new File([bytes], 'line.wav'), handle: null, path: 'line.wav' },
    ])
    const doc = workspace.activeDocument!
    await workspace.ensureChannels(doc)

    const states = [copy(doc.channels!)]
    const area: SpectralSelection = [
      {
        shape: { kind: 'rect', start: RATE, end: RATE * 1.5, low: 200, high: 4000 },
        subtract: false,
      },
    ]
    workspace.setSpectralSelection(doc, area)
    await workspace.applySpectral(doc, 'delete')
    states.push(copy(doc.channels!))
    await workspace.apply(doc, 'Gain', (channels) =>
      gain(channels, -6, { start: RATE * 5, end: RATE * 6 }),
    )
    states.push(copy(doc.channels!))
    await workspace.apply(doc, 'Delete', (channels) =>
      deleteRange(channels, { start: RATE * 8, end: RATE * 9 }),
    )
    states.push(copy(doc.channels!))
    workspace.addMarker(doc, { name: 'take', start: 0, length: RATE })
    states.push(copy(doc.channels!))

    expect(doc.undo.map((step) => step.label)).toEqual([
      'Delete spectral selection',
      'Gain',
      'Delete',
      'Add range marker',
    ])
    // A step keeps a few seconds at most, not the 20 s file.
    for (const step of doc.undo) expect(patchBytes(step.audio)).toBeLessThan(RATE * 4 * 2)
    expect(doc.undo.at(-1)!.audio).toBeNull()

    for (let index = states.length - 2; index >= 0; index--) {
      workspace.undo(doc)
      expect(copy(doc.channels!)).toEqual(states[index])
    }
    expect(doc.markers).toEqual([])
    // Back to where the first edit started, its area still selected to work on again.
    expect(doc.spectralSelection).toEqual(area)
    for (let index = 1; index < states.length; index++) {
      workspace.redo(doc)
      expect(copy(doc.channels!)).toEqual(states[index])
    }
    expect(doc.markers).toHaveLength(1)
  })
})
