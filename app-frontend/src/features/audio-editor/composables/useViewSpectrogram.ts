import { watchThrottled } from '@vueuse/core'
import { markRaw, shallowRef, watch } from 'vue'
import { runAnalysisJob } from '../lib/analysisClient'
import type { FrequencyAxis } from '../lib/frequencyScale'
import type { Channels } from '../lib/operations'
import { spectrogramInput, type Spectrogram, type SpectrogramRequest } from '../lib/spectrogram'

const MAX_COLUMNS = 4096
const MAX_ROWS = 512

interface Wanted {
  channels: Channels
  sampleRate: number
  request: SpectrogramRequest
}

/**
 * A spectrogram of just what is on screen, at one column per pixel, rebuilt as the view moves.
 * Zoomed out on a 2-hour take that is still one FFT per pixel rather than one per 1.8 s stretched
 * across the screen, and the worker only ever gets the few MB of windows it reads. While a new one
 * builds, the last one stays up, drawn where its frames now fall.
 */
export function useViewSpectrogram(options: {
  /** Changing it (another file) drops the spectrogram on screen. */
  key: () => unknown
  channels: () => Channels | null
  sampleRate: () => number
  view: () => { start: number; samplesPerPixel: number }
  /** Size in CSS pixels of the area it is drawn in. */
  size: () => { width: number; height: number }
  enabled: () => boolean
  /** How the image is analysed and laid out: window length, frequency range and dynamic range. */
  display: () => { fftSize: number; axis: FrequencyAxis; rangeDb: number }
}) {
  const current = shallowRef<Spectrogram | null>(null)
  let built: Wanted | null = null
  let running = false

  function wanted(): Wanted | null {
    const channels = options.channels()
    const sampleRate = options.sampleRate()
    const { width, height } = options.size()
    if (!options.enabled() || !channels || !sampleRate || width < 1 || height < 1) return null
    const { start, samplesPerPixel } = options.view()
    const frames = channels[0]?.length ?? 0
    const columns = Math.max(
      1,
      Math.min(MAX_COLUMNS, Math.ceil(width), Math.ceil((frames - start) / samplesPerPixel)),
    )
    const rows = Math.min(MAX_ROWS, Math.round(height * Math.min(2, window.devicePixelRatio || 1)))
    const { fftSize, axis, rangeDb } = options.display()
    return {
      channels,
      sampleRate,
      // A plain copy: requests go to the worker, which cannot take reactive proxies.
      request: { start, hop: samplesPerPixel, columns, rows, fftSize, axis: { ...axis }, rangeDb },
    }
  }

  function same(a: Wanted | null, b: Wanted | null) {
    if (!a || !b) return a === b
    return (
      a.channels === b.channels &&
      a.sampleRate === b.sampleRate &&
      a.request.start === b.request.start &&
      a.request.hop === b.request.hop &&
      a.request.columns === b.request.columns &&
      a.request.rows === b.request.rows &&
      a.request.fftSize === b.request.fftSize &&
      a.request.rangeDb === b.request.rangeDb &&
      a.request.axis.scale === b.request.axis.scale &&
      a.request.axis.low === b.request.axis.low &&
      a.request.axis.high === b.request.axis.high
    )
  }

  /** One build at a time; when it lands, build again if the view moved on meanwhile. */
  async function update() {
    if (running) return
    running = true
    try {
      for (let next = wanted(); next && !same(next, built); next = wanted()) {
        const key = options.key()
        const input = spectrogramInput(next.channels, next.request)
        const spectrogram = await runAnalysisJob(
          { kind: 'spectrogram', input, sampleRate: next.sampleRate, request: next.request },
          [input.samples.buffer],
        )
        built = next
        if (options.key() === key) current.value = markRaw(spectrogram)
      }
    } catch {
      // The view keeps the last spectrogram it had.
    } finally {
      running = false
    }
  }

  watch(options.key, () => {
    current.value = null
    built = null
  })

  watchThrottled(
    () => wanted(),
    () => void update(),
    { throttle: 60, trailing: true, immediate: true },
  )

  return current
}
