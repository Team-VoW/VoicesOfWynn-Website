import { defaultLayout, parseWav, WavError, type WavLayout } from './wav'

export interface DecodedAudio {
  sampleRate: number
  channels: Float32Array[]
  layout: WavLayout
  /** False for compressed sources: saving writes a new WAV rather than over the original. */
  savesInPlace: boolean
}

/** Browsers decode compressed audio at the context's rate; 48 kHz is what the mod ships. */
const COMPRESSED_SAMPLE_RATE = 48000

async function decodeWithBrowser(bytes: ArrayBuffer): Promise<DecodedAudio> {
  const context = new OfflineAudioContext(1, 1, COMPRESSED_SAMPLE_RATE)
  const buffer = await context.decodeAudioData(bytes)
  const channels = Array.from({ length: buffer.numberOfChannels }, (_, i) =>
    buffer.getChannelData(i).slice(),
  )
  return {
    sampleRate: buffer.sampleRate,
    channels,
    layout: defaultLayout(channels.length),
    savesInPlace: false,
  }
}

export async function decodeAudioFile(file: File): Promise<DecodedAudio> {
  const bytes = await file.arrayBuffer()
  if (file.name.toLowerCase().endsWith('.wav')) {
    try {
      const wav = parseWav(bytes)
      return { ...wav, savesInPlace: true }
    } catch (error) {
      // ADPCM and other encodings this parser does not write: the browser may still decode them.
      if (!(error instanceof WavError)) throw error
      return decodeWithBrowser(bytes)
    }
  }
  return decodeWithBrowser(bytes)
}
