// RIFF/WAVE reader and writer for destructive editing. Samples come out as one Float32Array per
// channel. Every chunk other than `fmt ` and `data` (LIST, bext, cue, iXML, …) is kept as raw bytes
// and written back in its original position, so saving over a file does not strip its metadata.

const FORMAT_PCM = 1
const FORMAT_FLOAT = 3
const FORMAT_EXTENSIBLE = 0xfffe

export type SampleFormat = 'pcm' | 'float'

export interface WavChunk {
  id: string
  /** Raw chunk body without the 8-byte header or pad byte. Absent for `fmt ` and `data`. */
  body?: Uint8Array
}

export interface WavLayout {
  format: SampleFormat
  bitDepth: number
  /** The original `fmt ` body, reused as is when the channel count still matches. */
  fmtBody: Uint8Array
  channelCount: number
  /** Chunk order of the source file; `fmt ` and `data` mark where those get regenerated. */
  chunks: WavChunk[]
}

export interface DecodedWav {
  sampleRate: number
  channels: Float32Array[]
  layout: WavLayout
}

export class WavError extends Error {}

function fourCC(view: DataView, offset: number) {
  return String.fromCharCode(
    view.getUint8(offset),
    view.getUint8(offset + 1),
    view.getUint8(offset + 2),
    view.getUint8(offset + 3),
  )
}

export function parseWav(buffer: ArrayBuffer): DecodedWav {
  const view = new DataView(buffer)
  if (buffer.byteLength < 12 || fourCC(view, 0) !== 'RIFF' || fourCC(view, 8) !== 'WAVE')
    throw new WavError('Not a RIFF/WAVE file.')

  const chunks: WavChunk[] = []
  let fmt: { body: Uint8Array; view: DataView } | null = null
  let data: { offset: number; size: number } | null = null
  let offset = 12
  while (offset + 8 <= buffer.byteLength) {
    const id = fourCC(view, offset)
    const declared = view.getUint32(offset + 4, true)
    const start = offset + 8
    // Recorders that crash mid-take leave a data size that runs past the end of the file.
    const size = Math.min(declared, buffer.byteLength - start)
    if (id === 'fmt ') {
      fmt = {
        body: new Uint8Array(buffer, start, size).slice(),
        view: new DataView(buffer, start, size),
      }
      chunks.push({ id })
    } else if (id === 'data') {
      data = { offset: start, size }
      chunks.push({ id })
    } else {
      chunks.push({ id, body: new Uint8Array(buffer, start, size).slice() })
    }
    offset = start + size + (size % 2)
  }
  if (!fmt || fmt.body.length < 16) throw new WavError('The WAV file has no format chunk.')
  if (!data) throw new WavError('The WAV file has no audio data.')

  let formatTag = fmt.view.getUint16(0, true)
  const channelCount = fmt.view.getUint16(2, true)
  const sampleRate = fmt.view.getUint32(4, true)
  const blockAlign = fmt.view.getUint16(12, true)
  const bitDepth = fmt.view.getUint16(14, true)
  if (formatTag === FORMAT_EXTENSIBLE && fmt.body.length >= 26)
    formatTag = fmt.view.getUint16(24, true)
  if (channelCount < 1) throw new WavError('The WAV file has no channels.')

  let format: SampleFormat
  if (formatTag === FORMAT_PCM && [8, 16, 24, 32].includes(bitDepth)) format = 'pcm'
  else if (formatTag === FORMAT_FLOAT && (bitDepth === 32 || bitDepth === 64)) format = 'float'
  else throw new WavError(`Unsupported WAV encoding (format ${formatTag}, ${bitDepth}-bit).`)

  const bytesPerSample = bitDepth / 8
  if (blockAlign !== bytesPerSample * channelCount)
    throw new WavError('The WAV format chunk is inconsistent.')
  const frames = Math.floor(data.size / blockAlign)
  const channels = Array.from({ length: channelCount }, () => new Float32Array(frames))
  const read = sampleReader(view, format, bitDepth)
  let position = data.offset
  for (let frame = 0; frame < frames; frame++) {
    for (let channel = 0; channel < channelCount; channel++) {
      channels[channel]![frame] = read(position)
      position += bytesPerSample
    }
  }

  return {
    sampleRate,
    channels,
    layout: { format, bitDepth, fmtBody: fmt.body, channelCount, chunks },
  }
}

function sampleReader(view: DataView, format: SampleFormat, bitDepth: number) {
  if (format === 'float')
    return bitDepth === 32
      ? (at: number) => view.getFloat32(at, true)
      : (at: number) => view.getFloat64(at, true)
  switch (bitDepth) {
    case 8:
      return (at: number) => (view.getUint8(at) - 128) / 128
    case 16:
      return (at: number) => view.getInt16(at, true) / 32768
    case 24:
      return (at: number) => {
        const value =
          view.getUint8(at) | (view.getUint8(at + 1) << 8) | (view.getInt8(at + 2) << 16)
        return value / 8388608
      }
    default:
      return (at: number) => view.getInt32(at, true) / 2147483648
  }
}

function sampleWriter(view: DataView, format: SampleFormat, bitDepth: number) {
  if (format === 'float')
    return bitDepth === 32
      ? (at: number, value: number) => view.setFloat32(at, value, true)
      : (at: number, value: number) => view.setFloat64(at, value, true)
  const quantize = (value: number, scale: number) =>
    Math.max(-scale, Math.min(scale - 1, Math.round(value * scale)))
  switch (bitDepth) {
    case 8:
      return (at: number, value: number) => view.setUint8(at, quantize(value, 128) + 128)
    case 16:
      return (at: number, value: number) => view.setInt16(at, quantize(value, 32768), true)
    case 24:
      return (at: number, value: number) => {
        const sample = quantize(value, 8388608)
        view.setUint8(at, sample & 0xff)
        view.setUint8(at + 1, (sample >> 8) & 0xff)
        view.setInt8(at + 2, sample >> 16)
      }
    default:
      return (at: number, value: number) => view.setInt32(at, quantize(value, 2147483648), true)
  }
}

/** A plain layout for audio that did not come from a WAV, or that changed channel count. */
export function defaultLayout(
  channelCount: number,
  format: SampleFormat = 'pcm',
  bitDepth = format === 'float' ? 32 : 16,
): WavLayout {
  return {
    format,
    bitDepth,
    fmtBody: new Uint8Array(0),
    channelCount,
    chunks: [{ id: 'fmt ' }, { id: 'data' }],
  }
}

function buildFmtBody(layout: WavLayout, channelCount: number, sampleRate: number) {
  if (layout.fmtBody.length >= 16 && layout.channelCount === channelCount) {
    const body = layout.fmtBody.slice()
    new DataView(body.buffer).setUint32(4, sampleRate, true)
    new DataView(body.buffer).setUint32(8, sampleRate * channelCount * (layout.bitDepth / 8), true)
    return body
  }
  const body = new Uint8Array(16)
  const view = new DataView(body.buffer)
  const blockAlign = channelCount * (layout.bitDepth / 8)
  view.setUint16(0, layout.format === 'float' ? FORMAT_FLOAT : FORMAT_PCM, true)
  view.setUint16(2, channelCount, true)
  view.setUint32(4, sampleRate, true)
  view.setUint32(8, sampleRate * blockAlign, true)
  view.setUint16(12, blockAlign, true)
  view.setUint16(14, layout.bitDepth, true)
  return body
}

export function encodeWav(channels: Float32Array[], sampleRate: number, layout: WavLayout) {
  const channelCount = channels.length
  const frames = channels[0]?.length ?? 0
  const bytesPerSample = layout.bitDepth / 8
  const dataSize = frames * channelCount * bytesPerSample
  const fmtBody = buildFmtBody(layout, channelCount, sampleRate)

  const parts = layout.chunks.map((chunk) => {
    if (chunk.id === 'fmt ') return { id: chunk.id, size: fmtBody.length, body: fmtBody }
    if (chunk.id === 'data') return { id: chunk.id, size: dataSize, body: null }
    let body = chunk.body ?? new Uint8Array(0)
    // `fact` carries the frame count of a float file; a stale one makes some players truncate.
    if (chunk.id === 'fact' && body.length >= 4) {
      body = body.slice()
      new DataView(body.buffer).setUint32(0, frames, true)
    }
    return { id: chunk.id, size: body.length, body }
  })
  const total = 12 + parts.reduce((sum, part) => sum + 8 + part.size + (part.size % 2), 0)
  const buffer = new ArrayBuffer(total)
  const view = new DataView(buffer)
  const bytes = new Uint8Array(buffer)
  const writeId = (at: number, id: string) => {
    for (let i = 0; i < 4; i++) view.setUint8(at + i, id.charCodeAt(i))
  }

  writeId(0, 'RIFF')
  view.setUint32(4, total - 8, true)
  writeId(8, 'WAVE')
  let offset = 12
  const write = sampleWriter(view, layout.format, layout.bitDepth)
  for (const part of parts) {
    writeId(offset, part.id)
    view.setUint32(offset + 4, part.size, true)
    offset += 8
    if (part.body) {
      bytes.set(part.body, offset)
    } else {
      let position = offset
      for (let frame = 0; frame < frames; frame++) {
        for (let channel = 0; channel < channelCount; channel++) {
          write(position, channels[channel]![frame]!)
          position += bytesPerSample
        }
      }
    }
    offset += part.size + (part.size % 2)
  }
  return buffer
}
