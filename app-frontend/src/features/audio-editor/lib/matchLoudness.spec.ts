import { describe, expect, it } from 'vitest'
import { matchLoudness, planMatch, type MatchOptions } from './matchLoudness'
import { analyze, integratedLoudness } from './loudness'
import { dbToGain } from './operations'

const RATE = 48000

function tone(seconds: number, dbfs: number, frequency = 440) {
  const amplitude = dbToGain(dbfs)
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / RATE),
  )
}

/** A steady voice with short loud syllables on top, peaky like a shouted line. */
function shouted() {
  const signal = tone(6, -24, 180)
  for (const start of [1, 2.5, 4, 5])
    for (let i = start * RATE; i < (start + 0.06) * RATE; i++) signal[i]! *= dbToGain(16)
  return signal
}

const options = (overrides: Partial<MatchOptions> = {}): MatchOptions => ({
  targetLufs: -18,
  ceilingDbtp: -1,
  peakControl: 'auto',
  ...overrides,
})

describe('matchLoudness', () => {
  it('gains a quiet line up to the target', () => {
    const result = matchLoudness([tone(5, -30)], RATE, options())

    expect(result.limited).toBe(false)
    expect(result.gainDb).toBeGreaterThan(0)
    expect(result.after.integratedLufs).toBeCloseTo(-18, 1)
  })

  it('limits instead of letting the true peak through the ceiling', () => {
    // A click on a quiet bed: reaching -13 LUFS would put the click far above 0 dBTP.
    const signal = tone(5, -30)
    for (let i = RATE * 2; i < RATE * 2 + 10; i++) signal[i] = 0.5

    const result = matchLoudness([signal], RATE, options({ targetLufs: -13 }))

    expect(result.limited).toBe(true)
    expect(result.after.truePeakDbtp).toBeLessThanOrEqual(-1)
    expect(result.after.integratedLufs).toBeCloseTo(-13, 1)
  })

  it('compresses hard pushes in Auto, taking the bulk of the work off the limiter', () => {
    const target = options({ targetLufs: -13 })
    const plan = planMatch(analyze([shouted()], RATE), target)
    expect(plan?.compresses).toBe(true)

    const auto = matchLoudness([shouted()], RATE, target)
    const limiterOnly = matchLoudness([shouted()], RATE, { ...target, peakControl: 'limit' })

    for (const result of [auto, limiterOnly]) {
      expect(result.after.truePeakDbtp).toBeLessThanOrEqual(-1)
      expect(result.after.integratedLufs).toBeCloseTo(-13, 1)
    }
    expect(auto.compressionDb).toBeGreaterThan(0)
    expect(auto.limitingDb).toBeLessThan(limiterOnly.limitingDb * 0.75)
    expect(limiterOnly.compressionDb).toBe(0)
  })

  it('stops pushing a line that cannot get louder under the ceiling', () => {
    // A steady tone already at the ceiling: more gain only squashes it, so no amount reaches -3.
    const signal = tone(5, -1.5)
    const before = integratedLoudness([signal], RATE)

    const result = matchLoudness([signal], RATE, options({ targetLufs: -3 }))

    expect(result.after.truePeakDbtp).toBeLessThanOrEqual(-1)
    expect(result.after.integratedLufs).toBeGreaterThan(before - 1)
    expect(result.gainDb).toBeLessThan(-3 - before + 13)
  })

  it('leaves silence alone', () => {
    const result = matchLoudness([new Float32Array(RATE)], RATE, options())

    expect(result.gainDb).toBe(0)
  })
})
