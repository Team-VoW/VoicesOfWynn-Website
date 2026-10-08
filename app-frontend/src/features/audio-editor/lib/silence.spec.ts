import { describe, expect, it } from 'vitest'
import { dbToGain } from './operations'
import {
  applyTrim,
  describeLoudEdges,
  edgeSoundAboveSilenceRule,
  loudEdges,
  planTrim,
  profileSilence,
} from './silence'

const RATE = 48000

let seed = 3
function noise(seconds: number, rmsDb: number) {
  // Uniform noise has an RMS of amplitude / sqrt(3).
  const amplitude = dbToGain(rmsDb) * Math.sqrt(3)
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    () => (((seed = (seed * 16807) % 2147483647) / 2147483647) * 2 - 1) * amplitude,
  )
}

function tone(seconds: number, rmsDb: number) {
  const amplitude = dbToGain(rmsDb) * Math.SQRT2
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * 220 * i) / RATE),
  )
}

/** Room noise, then a line, then room noise. */
function line(floorDb: number, lead = 0.5, speech = 1, tail = 0.6) {
  const room = (seconds: number) => noise(seconds, floorDb)
  const voice = tone(speech, -14)
  const bed = room(speech)
  for (let i = 0; i < voice.length; i++) voice[i]! += bed[i]!
  const parts = [room(lead), voice, room(tail)]
  const out = new Float32Array(parts.reduce((sum, part) => sum + part.length, 0))
  let offset = 0
  for (const part of parts) {
    out.set(part, offset)
    offset += part.length
  }
  return out
}

const keep = { keepLeadSeconds: 0.05, keepTailSeconds: 0.3 }

describe('profileSilence', () => {
  it('keeps to the -50 dB VoW rule on a clean recording', () => {
    const profile = profileSilence([line(-73)], RATE)

    expect(profile.noiseFloorDb).toBeCloseTo(-73, 0)
    expect(profile.speechDb).toBeCloseTo(-14, 0)
    expect(profile.suggestedThresholdDb).toBe(-50)
  })

  it('suggests 10 dB above a floor that sits close to -50 dB', () => {
    expect(profileSilence([line(-55)], RATE).suggestedThresholdDb).toBeCloseTo(-45, 0)
  })

  it('raises the threshold over a noisy room, but stays well under the speech', () => {
    const profile = profileSilence([line(-45)], RATE)

    expect(profile.noiseFloorDb).toBeCloseTo(-45, 0)
    // floor + 10 would be -35; 25 dB under -14 dB speech caps it at -39.
    expect(profile.suggestedThresholdDb).toBeCloseTo(-39, 0)
  })

  it('flags a line that starts on steady sound above -50 dB', () => {
    expect(edgeSoundAboveSilenceRule(profileSilence([line(-45)], RATE))).toBe(true)
    expect(edgeSoundAboveSilenceRule(profileSilence([line(-73)], RATE))).toBe(false)
  })

  it('says which edge sits on steady sound', () => {
    // Clean start, then the line, then a -42 dB hum to the end.
    const clip = line(-73)
    const hum = noise(0.4, -42)
    clip.set(hum, clip.length - hum.length)

    const profile = profileSilence([clip], RATE)

    expect(loudEdges(profile)).toEqual([{ edge: 'end', db: expect.closeTo(-42, 0) }])
    expect(describeLoudEdges(profile)).toBe('Ends on −42 dB sound')
  })

  it('does not take a quiet stretch after the start for background noise', () => {
    // Digital silence, then a breath-level stretch, then the line: the background is the silence.
    const breath = noise(0.3, -40)
    const voice = tone(1, -14)
    const clip = new Float32Array(RATE * 0.2 + breath.length + voice.length + RATE * 0.2)
    clip.set(breath, RATE * 0.2)
    clip.set(voice, RATE * 0.2 + breath.length)

    const profile = profileSilence([clip], RATE)

    expect(profile.noiseFloorDb).toBeLessThan(-100)
    expect(profile.suggestedThresholdDb).toBe(-50)
    expect(edgeSoundAboveSilenceRule(profile)).toBe(false)
  })

  it('does not go below -50 dB for digital silence', () => {
    const clip = new Float32Array(RATE * 2)
    clip.set(tone(1, -14), RATE / 2)

    expect(profileSilence([clip], RATE).suggestedThresholdDb).toBe(-50)
  })
})

describe('planTrim', () => {
  it('finds the silence in a noisy recording where a fixed -50 dB finds none', () => {
    const clip = line(-45)
    const suggested = profileSilence([clip], RATE).suggestedThresholdDb

    expect(planTrim([clip], RATE, { thresholdDb: -50, ...keep })!.removedLeadSeconds).toBe(0)
    const plan = planTrim([clip], RATE, { thresholdDb: suggested, ...keep })!
    expect(plan.removedLeadSeconds).toBeCloseTo(0.45, 1)
    expect(plan.removedTailSeconds).toBeCloseTo(0.3, 1)
  })

  it('is not stopped by a single click in the leading silence', () => {
    const clip = line(-80)
    for (let i = RATE * 0.1; i < RATE * 0.1 + RATE * 0.003; i++) clip[i] = 0.5

    const plan = planTrim([clip], RATE, { thresholdDb: -50, ...keep })!

    expect(plan.removedLeadSeconds).toBeCloseTo(0.45, 1)
  })

  it('never adds silence when the padding is already shorter', () => {
    const clip = line(-80, 0.02, 1, 0.1)

    const plan = planTrim([clip], RATE, { thresholdDb: -50, ...keep })!

    expect(plan.removedLeadSeconds).toBe(0)
    expect(plan.removedTailSeconds).toBe(0)
  })

  it('leaves a file alone when nothing rises above the threshold', () => {
    expect(planTrim([noise(1, -60)], RATE, { thresholdDb: -40, ...keep })).toBeNull()
  })
})

describe('applyTrim', () => {
  it('cuts to the plan and fades only the sides it cut', () => {
    const clip = line(-45, 0.5, 1, 0.1)
    const plan = planTrim([clip], RATE, { thresholdDb: -39, ...keep })!

    const [trimmed] = applyTrim([clip], RATE, plan)

    expect(trimmed).toHaveLength(plan.keep.end - plan.keep.start)
    expect(Math.abs(trimmed![0]!)).toBe(0)
    // The 0.1 s tail was under the 0.3 s padding, so the end was not cut and not faded.
    expect(trimmed![trimmed!.length - 1]).toBe(clip[clip.length - 1])
  })
})
