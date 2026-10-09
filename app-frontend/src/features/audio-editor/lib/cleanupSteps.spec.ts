import { describe, expect, it } from 'vitest'
import { averageSpectrum } from './averageSpectrum'
import { runCleanupChain, runCleanupStep, type CleanupStep } from './cleanupSteps'
import { eqResponseDb, equalize, type EqBand } from './filters'

const RATE = 48000

function tone(frequency: number, seconds = 0.5, amplitude = 0.5) {
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / RATE),
  )
}

function rms(channel: Float32Array, start = 0, end = channel.length) {
  let sum = 0
  for (let i = start; i < end; i++) sum += channel[i]! ** 2
  return Math.sqrt(sum / (end - start))
}

const bell: EqBand = { type: 'peaking', frequency: 1000, gainDb: -6, q: 1 }

describe('eqResponseDb', () => {
  it('is flat with no gain and hits the gain at a bell centre', () => {
    const [flat] = eqResponseDb([{ ...bell, gainDb: 0 }], RATE, [1000])
    const [centre, far] = eqResponseDb([bell], RATE, [1000, 50])
    expect(flat).toBeCloseTo(0, 6)
    expect(centre).toBeCloseTo(-6, 3)
    expect(Math.abs(far!)).toBeLessThan(0.1)
  })

  it('matches what equalize does to a tone', () => {
    const bands: EqBand[] = [
      { type: 'lowshelf', frequency: 200, gainDb: 4, q: Math.SQRT1_2 },
      { ...bell, frequency: 2500, gainDb: -5 },
    ]
    for (const frequency of [100, 700, 2500]) {
      const input = tone(frequency)
      const [output] = equalize([input], RATE, bands)
      const half = input.length / 2
      const measured = 20 * Math.log10(rms(output!, half) / rms(input, half))
      const [predicted] = eqResponseDb(bands, RATE, [frequency])
      expect(measured).toBeCloseTo(predicted!, 1)
    }
  })
})

describe('runCleanupStep', () => {
  it('only changes the selection, blending at its edges', () => {
    const input = tone(1000, 1)
    const step: CleanupStep = { kind: 'eq', bands: [{ ...bell, gainDb: -12 }] }
    const { channels } = runCleanupStep([input], RATE, step, { start: 24000, end: 48000 })
    const [output] = channels
    expect(rms(output!, 0, 23000)).toBeCloseTo(rms(input, 0, 23000), 6)
    expect(rms(output!, 30000, 48000) / rms(input, 30000, 48000)).toBeLessThan(0.3)
  })

  it('counts repaired clicks', () => {
    const input = tone(200, 0.5, 0.1)
    input[12000] = 0.9
    const { clicks } = runCleanupStep([input], RATE, { kind: 'clicks', threshold: 12 })
    expect(clicks).toBeGreaterThan(0)
  })
})

describe('runCleanupChain', () => {
  it('skips a noise print taken at another rate and runs the rest', () => {
    const input = tone(1000)
    const { channels, skipped } = runCleanupChain([input], RATE, [
      {
        kind: 'noise',
        print: { sampleRate: 44100, magnitude: new Float32Array(1025), seconds: 1, source: 'x' },
        options: { reductionDb: 12, strength: 1.5 },
      },
      { kind: 'eq', bands: [bell] },
    ])
    expect(skipped).toEqual(['noise'])
    expect(rms(channels[0]!) / rms(input)).toBeLessThan(0.6)
  })
})

describe('averageSpectrum', () => {
  it('peaks at the frequency of a tone', () => {
    const { frequencies, levelsDb } = averageSpectrum([tone(1500, 1)], RATE)
    let loudest = 0
    for (let i = 1; i < levelsDb.length; i++) if (levelsDb[i]! > levelsDb[loudest]!) loudest = i
    expect(frequencies[loudest]).toBeGreaterThan(1350)
    expect(frequencies[loudest]).toBeLessThan(1650)
  })

  it('handles a selection shorter than one analysis window', () => {
    const { levelsDb } = averageSpectrum([tone(1000)], RATE, { start: 100, end: 600 })
    expect(levelsDb.every(Number.isFinite)).toBe(true)
  })
})
