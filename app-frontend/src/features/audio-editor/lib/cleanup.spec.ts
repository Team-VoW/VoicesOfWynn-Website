import { describe, expect, it } from 'vitest'
import { removeClicks } from './clicks'
import { compress, deEss, equalize, highPass, removeHum, sibilancePeakDb } from './filters'
import { captureNoisePrint, reduceNoise } from './noiseReduction'

const RATE = 48000

function tone(frequency: number, seconds = 0.5, amplitude = 0.5) {
  return Float32Array.from(
    { length: Math.round(seconds * RATE) },
    (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / RATE),
  )
}

/** RMS over the second half, after any filter has settled. */
function rms(channel: Float32Array) {
  const half = channel.subarray(Math.floor(channel.length / 2))
  return Math.sqrt(half.reduce((sum, value) => sum + value * value, 0) / half.length)
}

function seededNoise(length: number, amplitude: number, seed = 1) {
  let state = seed
  return Float32Array.from({ length }, () => {
    state = (state * 1664525 + 1013904223) >>> 0
    return amplitude * (state / 2 ** 32 - 0.5) * 2
  })
}

describe('highPass', () => {
  it('removes rumble and keeps the voice', () => {
    const [rumble] = highPass([tone(30)], RATE, 80, 24)
    const [voice] = highPass([tone(1000)], RATE, 80, 24)
    expect(rms(rumble!) / rms(tone(30))).toBeLessThan(0.1)
    expect(rms(voice!) / rms(tone(1000))).toBeGreaterThan(0.98)
  })
})

describe('removeHum', () => {
  it('cuts the mains frequency and its harmonics only', () => {
    const [hum] = removeHum([tone(50)], RATE, 50, 4, 30)
    const [harmonic] = removeHum([tone(150)], RATE, 50, 4, 30)
    const [voice] = removeHum([tone(1000)], RATE, 50, 4, 30)
    expect(rms(hum!) / rms(tone(50))).toBeLessThan(0.1)
    expect(rms(harmonic!) / rms(tone(150))).toBeLessThan(0.1)
    expect(rms(voice!) / rms(tone(1000))).toBeGreaterThan(0.98)
  })
})

describe('equalize', () => {
  it('boosts a peaking band by its gain', () => {
    const [boosted] = equalize([tone(1000)], RATE, [
      { type: 'peaking', frequency: 1000, gainDb: 6, q: 1 },
    ])
    expect(20 * Math.log10(rms(boosted!) / rms(tone(1000)))).toBeCloseTo(6, 0)
  })
})

describe('deEss', () => {
  it('turns down loud sibilance and leaves the low voice alone', () => {
    const options = { frequency: 5000, thresholdDb: -30, maxReductionDb: 12 }
    const [ess] = deEss([tone(8000)], RATE, options)
    const [voice] = deEss([tone(300)], RATE, options)
    expect(rms(ess!) / rms(tone(8000))).toBeLessThan(0.4)
    expect(rms(voice!) / rms(tone(300))).toBeGreaterThan(0.97)
  })

  it('measures the sibilance band', () => {
    expect(sibilancePeakDb([tone(8000)], RATE, 5000)).toBeGreaterThan(-8)
    expect(sibilancePeakDb([tone(200)], RATE, 5000)).toBeLessThan(-40)
  })
})

describe('compress', () => {
  it('reduces level above the threshold by the ratio', () => {
    // A 0.5 peak is about −6 dB: 14 dB over −20, so 4:1 leaves 3.5 dB over.
    const [output] = compress([tone(1000)], RATE, {
      thresholdDb: -20,
      ratio: 4,
      attackMs: 1,
      releaseMs: 50,
      kneeDb: 0,
      makeupDb: 0,
    })
    const peak = Math.max(...output!.subarray(RATE / 4).map(Math.abs))
    expect(20 * Math.log10(peak)).toBeCloseTo(-16.5, 0)
  })
})

describe('reduceNoise', () => {
  it('lowers the noise by about the requested amount and keeps the tone', () => {
    const noise = seededNoise(RATE, 0.02)
    const signal = tone(1000, 1, 0.3)
    const mixed = Float32Array.from(noise, (value, i) => value + (i >= RATE / 2 ? signal[i]! : 0))
    const print = captureNoisePrint([mixed], RATE, { start: 0, end: RATE / 2 }, 'test')
    const [output] = reduceNoise([mixed], RATE, print, { reductionDb: 20, strength: 2 })

    const noiseBefore = rms(mixed.subarray(4096, RATE / 2 - 4096))
    const noiseAfter = rms(output!.subarray(4096, RATE / 2 - 4096))
    expect(20 * Math.log10(noiseAfter / noiseBefore)).toBeLessThan(-12)
    const toneAfter = rms(output!.subarray(RATE / 2 + 4096))
    expect(toneAfter / rms(signal.subarray(RATE / 2 + 4096))).toBeGreaterThan(0.9)
  })

  it('refuses a print from another sample rate', () => {
    const print = captureNoisePrint([tone(100)], RATE, { start: 0, end: RATE / 4 }, 'test')
    expect(() => reduceNoise([tone(100)], 44100, print, { reductionDb: 12, strength: 2 })).toThrow()
  })

  it('asks for a longer selection than one window', () => {
    expect(() => captureNoisePrint([tone(100)], RATE, { start: 0, end: 100 }, 'test')).toThrow(
      /at least/,
    )
  })
})

describe('removeClicks', () => {
  const options = { threshold: 12, maxClickMs: 2 }

  it('repairs a click back to the waveform under it', () => {
    const clean = tone(440, 0.25)
    const clicked = clean.slice()
    clicked[5000]! += 0.8
    clicked[5001]! -= 0.5
    const { channels, clicks } = removeClicks([clicked], RATE, options)
    expect(clicks).toBe(1)
    let error = 0
    for (let i = 0; i < clean.length; i++)
      error = Math.max(error, Math.abs(channels[0]![i]! - clean[i]!))
    expect(error).toBeLessThan(0.01)
  })

  it('leaves smooth audio and noise alone', () => {
    expect(removeClicks([tone(440)], RATE, options).clicks).toBe(0)
    expect(removeClicks([seededNoise(RATE / 2, 0.1)], RATE, options).clicks).toBe(0)
  })
})
