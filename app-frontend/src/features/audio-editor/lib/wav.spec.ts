import { describe, expect, it } from 'vitest'
import { defaultLayout, encodeWav, parseWav, WavError } from './wav'

function sine(frames: number, amplitude = 0.5) {
  return Float32Array.from({ length: frames }, (_, i) => amplitude * Math.sin(i / 7))
}

function chunk(id: string, body: Uint8Array) {
  const bytes = new Uint8Array(8 + body.length + (body.length % 2))
  const view = new DataView(bytes.buffer)
  for (let i = 0; i < 4; i++) bytes[i] = id.charCodeAt(i)
  view.setUint32(4, body.length, true)
  bytes.set(body, 8)
  return bytes
}

describe('wav', () => {
  it.each([
    ['pcm', 16],
    ['pcm', 24],
    ['pcm', 8],
    ['float', 32],
  ] as const)('round-trips %s %i-bit byte for byte', (format, bitDepth) => {
    const original = encodeWav(
      [sine(1001), sine(1001, 0.25)],
      44100,
      defaultLayout(2, format, bitDepth),
    )

    const decoded = parseWav(original)
    const again = encodeWav(decoded.channels, decoded.sampleRate, decoded.layout)

    expect(decoded.sampleRate).toBe(44100)
    expect(decoded.layout.bitDepth).toBe(bitDepth)
    expect(decoded.channels).toHaveLength(2)
    expect(new Uint8Array(again)).toEqual(new Uint8Array(original))
  })

  it('keeps metadata chunks in place and updates fact to the new length', () => {
    const base = new Uint8Array(encodeWav([sine(10)], 48000, defaultLayout(1, 'float')))
    const list = chunk(
      'LIST',
      new TextEncoder().encode('INFOISFT\u0005\u0000\u0000\u0000vow!\u0000'),
    )
    const fact = chunk('fact', new Uint8Array([10, 0, 0, 0]))
    // RIFF header, fmt (8 + 16), then our two chunks, then data.
    const fmtEnd = 12 + 8 + 16
    const file = new Uint8Array(base.length + list.length + fact.length)
    file.set(base.subarray(0, fmtEnd), 0)
    file.set(list, fmtEnd)
    file.set(fact, fmtEnd + list.length)
    file.set(base.subarray(fmtEnd), fmtEnd + list.length + fact.length)
    new DataView(file.buffer).setUint32(4, file.length - 8, true)

    const decoded = parseWav(file.buffer)
    expect(decoded.layout.chunks.map((c) => c.id)).toEqual(['fmt ', 'LIST', 'fact', 'data'])

    const shorter = decoded.channels.map((c) => c.slice(0, 4))
    const reparsed = parseWav(encodeWav(shorter, decoded.sampleRate, decoded.layout))
    expect(reparsed.layout.chunks.map((c) => c.id)).toEqual(['fmt ', 'LIST', 'fact', 'data'])
    expect(reparsed.layout.chunks[1]!.body).toEqual(decoded.layout.chunks[1]!.body)
    expect(new DataView(reparsed.layout.chunks[2]!.body!.buffer).getUint32(0, true)).toBe(4)
    expect(reparsed.channels[0]).toHaveLength(4)
  })

  it('writes a fresh format chunk when the channel count changes', () => {
    const decoded = parseWav(encodeWav([sine(20), sine(20)], 48000, defaultLayout(2)))

    const mono = parseWav(encodeWav([decoded.channels[0]!], 48000, decoded.layout))

    expect(mono.channels).toHaveLength(1)
    expect(mono.channels[0]).toEqual(decoded.channels[0])
  })

  it('clips out-of-range samples instead of wrapping them', () => {
    const decoded = parseWav(
      encodeWav([Float32Array.from([1.5, -1.5, 0])], 48000, defaultLayout(1, 'pcm', 16)),
    )

    expect(decoded.channels[0]![0]).toBeCloseTo(32767 / 32768)
    expect(decoded.channels[0]![1]).toBe(-1)
  })

  it('rejects files that are not WAV', () => {
    expect(() => parseWav(new TextEncoder().encode('OggS not a wav file').buffer)).toThrow(WavError)
  })
})
