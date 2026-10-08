import { describe, expect, it } from 'vitest'
import { analyze, integratedLoudness, matchLoudness, truePeak, truePeakEnvelope } from './loudness'
import { dbToGain } from './operations'

const RATE = 48000

function tone(seconds: number, dbfs: number, frequency = 1000, phase = 0) {
  const amplitude = dbToGain(dbfs)
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / RATE + phase),
  )
}

describe('loudness', () => {
  // EBU Tech 3341 case 1: a stereo 1 kHz sine at -23 dBFS reads -23 LUFS.
  it('measures the EBU reference tone at -23 LUFS', () => {
    const lufs = integratedLoudness([tone(20, -23), tone(20, -23)], RATE)

    expect(lufs).toBeCloseTo(-23, 1)
  })

  it('reads the same tone in mono 3 LU quieter, as ffmpeg does', () => {
    expect(integratedLoudness([tone(10, -23)], RATE)).toBeCloseTo(-26.01, 1)
  })

  it('gates out silence so pauses do not drag the reading down', () => {
    const withGap = new Float32Array(RATE * 20)
    withGap.set(tone(10, -20), 0)

    // Ungated, ten seconds of silence would pull this down by 3 LU; only the one block that
    // straddles the edge of the tone is allowed to nudge it.
    const difference =
      integratedLoudness([tone(10, -20)], RATE) - integratedLoudness([withGap], RATE)
    expect(Math.abs(difference)).toBeLessThan(0.1)
  })

  it('measures clips shorter than one gating block as a single block', () => {
    // Same level as a long tone of the same signal, rather than no reading at all.
    expect(integratedLoudness([tone(0.3, -10)], RATE)).toBeCloseTo(
      integratedLoudness([tone(10, -10)], RATE),
      0,
    )
    expect(integratedLoudness([new Float32Array(RATE * 0.3)], RATE)).toBe(-Infinity)
  })

  it('finds the inter-sample peak that sample peak misses', () => {
    // fs/4 at a 45° offset: every sample sits at ±0.707 while the waveform peaks at 1.0.
    const signal = tone(1, 0, RATE / 4, Math.PI / 4)

    expect(Math.max(...signal)).toBeCloseTo(Math.SQRT1_2, 3)
    expect(truePeak([signal])).toBeGreaterThan(-0.3)
  })

  it('skips quiet blocks without changing the true peak it reports', () => {
    let seed = 7
    const random = () => ((seed = (seed * 16807) % 2147483647) / 2147483647) * 2 - 1
    for (let trial = 0; trial < 20; trial++) {
      // Quiet noise with a few loud bursts, the shape of a voice line.
      const signal = Float32Array.from({ length: 4000 }, () => random() * 0.02)
      for (let burst = 0; burst < 3; burst++) {
        const at = Math.floor(Math.abs(random()) * 3900)
        for (let i = at; i < at + 40; i++) signal[i] = random() * 0.9
      }

      const exhaustive = Math.max(...truePeakEnvelope([signal]))

      // The envelope is Float32, so allow its rounding (~1e-6 dB).
      expect(truePeak([signal])).toBeCloseTo(20 * Math.log10(exhaustive), 5)
    }
  })

  it('measures head and tail silence against the -50 dB threshold', () => {
    const clip = new Float32Array(RATE)
    clip.set(tone(0.5, -12), RATE * 0.2)

    const result = analyze([clip], RATE)

    expect(result.leadingSilenceSeconds).toBeCloseTo(0.2, 2)
    expect(result.trailingSilenceSeconds).toBeCloseTo(0.3, 2)
  })
})

describe('matchLoudness', () => {
  it('gains a quiet line up to the target', () => {
    const result = matchLoudness([tone(5, -30, 440)], RATE, -18, -1)

    expect(result.limited).toBe(false)
    expect(result.gainDb).toBeGreaterThan(0)
    expect(result.after.integratedLufs).toBeCloseTo(-18, 1)
  })

  it('limits instead of letting the true peak through the ceiling', () => {
    // A click on a quiet bed: reaching -13 LUFS would put the click far above 0 dBTP.
    const signal = tone(5, -30, 440)
    for (let i = RATE * 2; i < RATE * 2 + 10; i++) signal[i] = 0.5

    const result = matchLoudness([signal], RATE, -13, -1)

    expect(result.limited).toBe(true)
    expect(result.after.truePeakDbtp).toBeLessThanOrEqual(-1)
    expect(result.after.integratedLufs).toBeCloseTo(-13, 0)
  })

  it('leaves silence alone', () => {
    const result = matchLoudness([new Float32Array(RATE)], RATE, -18, -1)

    expect(result.gainDb).toBe(0)
  })
})
