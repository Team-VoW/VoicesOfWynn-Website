// Markers as Audition keeps them in a WAV. It writes every marker three times: a `cue ` point, an
// entry in the `LIST`/`adtl` chunk (`labl` name, `note` description, `ltxt` length for a range) and
// an item in the XMP packet (`_PMX`), which is what Audition reads back first. Other editors (Reaper,
// Wavelab, Sound Forge) only use the RIFF chunks. Reading prefers the XMP and falls back to the
// chunks; writing regenerates all three so they never disagree.

import type { WavChunk, WavLayout } from './wav'

export interface MarkerData {
  name: string
  /** First frame. */
  start: number
  /** Frames covered; 0 for a cue (point) marker. */
  length: number
  /** Audition's marker description. */
  comment: string
  /** Audition's id for the marker, kept so it recognises it after a save. */
  guid: string | null
}

export interface Marker extends MarkerData {
  id: number
}

/** A splice: at `start`, `removed` frames were replaced by `inserted` new ones. */
export interface Splice {
  start: number
  removed: number
  inserted: number
}

export function sortMarkers<T extends MarkerData>(markers: T[]) {
  return markers.slice().sort((a, b) => a.start - b.start || a.length - b.length)
}

/**
 * Where markers end up after an edit that changed the length, the way Audition moves them: those
 * after the edit shift, a range spanning it stretches or shrinks, and ones entirely inside cut
 * audio are dropped.
 */
export function spliceMarkers<T extends MarkerData>(markers: T[], splice: Splice): T[] {
  const { start, removed, inserted } = splice
  const removedEnd = start + removed
  const mapStart = (frame: number) =>
    frame < start ? frame : frame >= removedEnd ? frame - removed + inserted : start
  const mapEnd = (frame: number) =>
    frame <= start ? frame : frame >= removedEnd ? frame - removed + inserted : start + inserted
  const result: T[] = []
  for (const marker of markers) {
    if (marker.length === 0) {
      if (marker.start > start && marker.start < removedEnd) continue
      result.push({ ...marker, start: mapStart(marker.start) })
      continue
    }
    const from = mapStart(marker.start)
    const to = mapEnd(marker.start + marker.length)
    if (to <= from) continue
    result.push({ ...marker, start: from, length: to - from })
  }
  return result
}

/** The splices of cropping `frames` of audio down to `keep`. */
export function cropSplices(frames: number, keep: { start: number; end: number }): Splice[] {
  return [
    { start: keep.end, removed: Math.max(0, frames - keep.end), inserted: 0 },
    { start: 0, removed: keep.start, inserted: 0 },
  ]
}

/** Keeps markers within `frames`, for edits whose effect on timing is not known. */
export function clampMarkers<T extends MarkerData>(markers: T[], frames: number): T[] {
  return spliceMarkers(markers, {
    start: frames,
    removed: Math.max(0, markerEnd(markers) - frames),
    inserted: 0,
  })
}

function markerEnd(markers: MarkerData[]) {
  return markers.reduce((end, marker) => Math.max(end, marker.start + marker.length), 0)
}

// ---------------------------------------------------------------------------------------------
// RIFF chunks

const utf8 = new TextDecoder('utf-8', { fatal: true })
const latin1 = new TextDecoder('latin1')
const encoder = new TextEncoder()

function readString(bytes: Uint8Array) {
  const end = bytes.indexOf(0)
  const body = end >= 0 ? bytes.subarray(0, end) : bytes
  try {
    return utf8.decode(body)
  } catch {
    return latin1.decode(body)
  }
}

function fourCC(bytes: Uint8Array, offset: number) {
  return String.fromCharCode(...bytes.subarray(offset, offset + 4))
}

function isAdtl(chunk: WavChunk) {
  return chunk.id === 'LIST' && !!chunk.body && fourCC(chunk.body, 0) === 'adtl'
}

function readChunkMarkers(chunks: WavChunk[]): MarkerData[] {
  const cue = chunks.find((chunk) => chunk.id === 'cue ')?.body
  if (!cue || cue.length < 4) return []
  const view = new DataView(cue.buffer, cue.byteOffset, cue.byteLength)
  const points = new Map<number, number>()
  const count = Math.min(view.getUint32(0, true), Math.floor((cue.length - 4) / 24))
  for (let i = 0; i < count; i++) {
    const at = 4 + i * 24
    points.set(view.getUint32(at, true), view.getUint32(at + 20, true))
  }

  const names = new Map<number, string>()
  const notes = new Map<number, string>()
  const lengths = new Map<number, number>()
  for (const chunk of chunks) {
    if (!isAdtl(chunk)) continue
    const body = chunk.body!
    const list = new DataView(body.buffer, body.byteOffset, body.byteLength)
    let offset = 4
    while (offset + 8 <= body.length) {
      const id = fourCC(body, offset)
      const size = Math.min(list.getUint32(offset + 4, true), body.length - offset - 8)
      const sub = body.subarray(offset + 8, offset + 8 + size)
      if (size >= 4) {
        const cueId = list.getUint32(offset + 8, true)
        if (id === 'labl') names.set(cueId, readString(sub.subarray(4)))
        else if (id === 'note') notes.set(cueId, readString(sub.subarray(4)))
        else if (id === 'ltxt' && size >= 8) lengths.set(cueId, list.getUint32(offset + 12, true))
      }
      offset += 8 + size + (size % 2)
    }
  }

  return sortMarkers(
    [...points].map(([cueId, start]) => ({
      name: names.get(cueId) ?? '',
      start,
      length: lengths.get(cueId) ?? 0,
      comment: notes.get(cueId) ?? '',
      guid: null,
    })),
  )
}

function stringChunk(id: string, cueId: number, text: string) {
  const encoded = encoder.encode(text)
  const body = new Uint8Array(4 + encoded.length + 1)
  new DataView(body.buffer).setUint32(0, cueId, true)
  body.set(encoded, 4)
  return subChunk(id, body)
}

function subChunk(id: string, body: Uint8Array) {
  const bytes = new Uint8Array(8 + body.length + (body.length % 2))
  for (let i = 0; i < 4; i++) bytes[i] = id.charCodeAt(i)
  new DataView(bytes.buffer).setUint32(4, body.length, true)
  bytes.set(body, 8)
  return bytes
}

function concat(parts: Uint8Array[]) {
  const output = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0))
  let offset = 0
  for (const part of parts) {
    output.set(part, offset)
    offset += part.length
  }
  return output
}

function cueChunk(markers: MarkerData[]) {
  const body = new Uint8Array(4 + markers.length * 24)
  const view = new DataView(body.buffer)
  view.setUint32(0, markers.length, true)
  markers.forEach((marker, index) => {
    const at = 4 + index * 24
    view.setUint32(at, index + 1, true)
    view.setUint32(at + 4, marker.start, true)
    for (let i = 0; i < 4; i++) body[at + 8 + i] = 'data'.charCodeAt(i)
    view.setUint32(at + 20, marker.start, true)
  })
  return body
}

function adtlChunk(markers: MarkerData[]) {
  const parts = [encoder.encode('adtl')]
  markers.forEach((marker, index) => {
    const cueId = index + 1
    parts.push(stringChunk('labl', cueId, marker.name))
    if (marker.comment) parts.push(stringChunk('note', cueId, marker.comment))
    if (marker.length > 0) {
      const body = new Uint8Array(20)
      const view = new DataView(body.buffer)
      view.setUint32(0, cueId, true)
      view.setUint32(4, marker.length, true)
      for (let i = 0; i < 4; i++) body[8 + i] = 'rgn '.charCodeAt(i)
      parts.push(subChunk('ltxt', body))
    }
  })
  return concat(parts)
}

// ---------------------------------------------------------------------------------------------
// XMP

const XMP_DM = 'http://ns.adobe.com/xmp/1.0/DynamicMedia/'

/** The [start, end) of each `<rdf:li>` directly inside `xml[from, to)`. */
function topLevelItems(xml: string, from: number, to: number) {
  const items: { start: number; end: number }[] = []
  const tag = /<(\/?)rdf:li\b[^>]*?(\/?)>/g
  tag.lastIndex = from
  let depth = 0
  let open = 0
  for (let match = tag.exec(xml); match && match.index < to; match = tag.exec(xml)) {
    const closing = match[1] === '/'
    const selfClosing = match[2] === '/'
    if (!closing && depth === 0) open = match.index
    if (selfClosing) {
      if (depth === 0) items.push({ start: open, end: tag.lastIndex })
    } else if (closing) {
      depth--
      if (depth === 0) items.push({ start: open, end: tag.lastIndex })
    } else {
      depth++
    }
  }
  return items
}

/** The span of the first `<name>…</name>` (or `<name/>`) in `xml[from, to)`, nesting included. */
function element(xml: string, name: string, from = 0, to = xml.length) {
  const tag = new RegExp(`<(/?)${name}(?:\\s[^>]*?)?(/?)>`, 'g')
  tag.lastIndex = from
  const first = tag.exec(xml)
  if (!first || first.index >= to || first[1] === '/') return null
  if (first[2] === '/') return { start: first.index, end: tag.lastIndex, inner: null }
  const innerStart = tag.lastIndex
  let depth = 1
  for (let match = tag.exec(xml); match && match.index < to; match = tag.exec(xml)) {
    if (match[2] === '/') continue
    depth += match[1] === '/' ? -1 : 1
    if (depth === 0)
      return {
        start: first.index,
        end: tag.lastIndex,
        inner: { start: innerStart, end: match.index },
      }
  }
  return null
}

function unescapeXml(text: string) {
  return text.replace(/&(#x[0-9a-f]+|#\d+|amp|lt|gt|quot|apos);/gi, (_, entity: string) => {
    const lower = entity.toLowerCase()
    if (lower === 'amp') return '&'
    if (lower === 'lt') return '<'
    if (lower === 'gt') return '>'
    if (lower === 'quot') return '"'
    if (lower === 'apos') return "'"
    return String.fromCodePoint(
      lower.startsWith('#x') ? parseInt(lower.slice(2), 16) : parseInt(lower.slice(1), 10),
    )
  })
}

function escapeXml(text: string) {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** A property written either as `<xmpDM:x>value</xmpDM:x>` or as an `xmpDM:x="value"` attribute. */
function property(xml: string, name: string) {
  const match = new RegExp(`<xmpDM:${name}>([^<]*)</xmpDM:${name}>|xmpDM:${name}="([^"]*)"`).exec(
    xml,
  )
  return match ? unescapeXml(match[1] ?? match[2] ?? '') : null
}

/** `f44100` is 44100 per second; `f30000s1001` is 30000/1001. */
function frameRate(xml: string) {
  const match = /^f(\d+)(?:s(\d+))?$/.exec(property(xml, 'frameRate') ?? '')
  return match ? Number(match[1]) / Number(match[2] ?? 1) : null
}

type Span = NonNullable<ReturnType<typeof element>>

/** The Tracks element, its bag, and the bag's `<rdf:li>` holding the cue markers. */
function findCueTrack(xml: string): {
  tracks: Span | null
  bag: Span | null
  track: { start: number; end: number } | null
} {
  const tracks = element(xml, 'xmpDM:Tracks')
  if (!tracks?.inner) return { tracks, bag: null, track: null }
  const bag = element(xml, 'rdf:Bag', tracks.inner.start, tracks.inner.end)
  if (!bag?.inner) return { tracks, bag, track: null }
  for (const item of topLevelItems(xml, bag.inner.start, bag.inner.end)) {
    const text = xml.slice(item.start, item.end)
    const markers = element(text, 'xmpDM:markers')
    const own = markers ? text.slice(0, markers.start) + text.slice(markers.end) : text
    if (property(own, 'trackType') === 'Cue') return { tracks, bag, track: item }
  }
  return { tracks, bag, track: null }
}

function readXmpMarkers(xml: string, sampleRate: number): MarkerData[] | null {
  const { track } = findCueTrack(xml)
  if (!track) return null
  const text = xml.slice(track.start, track.end)
  const markers = element(text, 'xmpDM:markers')
  const seq = markers?.inner
    ? element(text, 'rdf:Seq', markers.inner.start, markers.inner.end)
    : null
  if (!seq?.inner) return null
  const rate = frameRate(text) ?? sampleRate
  const toFrames = (value: string | null) =>
    Math.max(0, Math.round((Number(value ?? 0) * sampleRate) / rate) || 0)
  const result: MarkerData[] = []
  for (const item of topLevelItems(text, seq.inner.start, seq.inner.end)) {
    const marker = text.slice(item.start, item.end)
    // The nested cuePointParams items carry no marker fields, but strip them to be safe.
    const params = element(marker, 'xmpDM:cuePointParams')
    const own = params ? marker.slice(0, params.start) + marker.slice(params.end) : marker
    if (property(own, 'startTime') === null) continue
    result.push({
      name: property(own, 'name') ?? '',
      start: toFrames(property(own, 'startTime')),
      length: toFrames(property(own, 'duration')),
      comment: property(own, 'comment') ?? '',
      guid: property(own, 'guid'),
    })
  }
  return sortMarkers(result)
}

function newGuid() {
  return `xmp:id:${crypto.randomUUID()}`
}

function xmpMarkers(markers: MarkerData[], indent: string) {
  const pad = (depth: number) => `\n${indent}${'   '.repeat(depth)}`
  const items = markers.map((marker) => {
    const guid = marker.guid ?? newGuid()
    const fields = [
      `<xmpDM:startTime>${marker.start}</xmpDM:startTime>`,
      marker.length > 0 ? `<xmpDM:duration>${marker.length}</xmpDM:duration>` : null,
      marker.comment ? `<xmpDM:comment>${escapeXml(marker.comment)}</xmpDM:comment>` : null,
      `<xmpDM:name>${escapeXml(marker.name)}</xmpDM:name>`,
      `<xmpDM:cuePointParams>${pad(4)}<rdf:Seq>${pad(5)}<rdf:li rdf:parseType="Resource">${pad(6)}<xmpDM:key>marker_guid</xmpDM:key>${pad(6)}<xmpDM:value>${guid}</xmpDM:value>${pad(5)}</rdf:li>${pad(4)}</rdf:Seq>${pad(3)}</xmpDM:cuePointParams>`,
      `<xmpDM:guid>${guid}</xmpDM:guid>`,
    ].filter(Boolean)
    return `${pad(2)}<rdf:li rdf:parseType="Resource">${fields.map((field) => `${pad(3)}${field}`).join('')}${pad(2)}</rdf:li>`
  })
  return `<xmpDM:markers>${pad(1)}<rdf:Seq>${items.join('')}${pad(1)}</rdf:Seq>${pad(0)}</xmpDM:markers>`
}

function indentBefore(xml: string, at: number) {
  const lineStart = xml.lastIndexOf('\n', at - 1) + 1
  return /^[ \t]*/.exec(xml.slice(lineStart, at))![0]
}

/**
 * The packet with its cue track's markers replaced, or null when there is nowhere sensible to put
 * them. Everything else in the packet is left exactly as it was.
 */
export function rewriteXmpMarkers(xml: string, markers: MarkerData[], sampleRate: number) {
  const { tracks, bag, track } = findCueTrack(xml)
  if (track) {
    let text = xml.slice(track.start, track.end)
    const rate = element(text, 'xmpDM:frameRate')
    if (rate)
      text = `${text.slice(0, rate.start)}<xmpDM:frameRate>f${sampleRate}</xmpDM:frameRate>${text.slice(rate.end)}`
    const existing = element(text, 'xmpDM:markers')
    const close = text.lastIndexOf('</rdf:li>')
    const indent = existing ? indentBefore(text, existing.start) : `${indentBefore(text, close)}   `
    const block = markers.length ? xmpMarkers(markers, indent) : ''
    if (existing) {
      // Drop the line the old block stood on along with it when nothing replaces it.
      const from = block ? existing.start : text.lastIndexOf('\n', existing.start - 1)
      text = text.slice(0, from) + block + text.slice(existing.end)
    } else if (block) {
      text = `${text.slice(0, close)}   ${block}\n${indentBefore(text, close)}${text.slice(close)}`
    }
    return xml.slice(0, track.start) + text + xml.slice(track.end)
  }
  if (markers.length === 0) return xml
  if (bag?.inner) {
    const indent = indentBefore(xml, bag.inner.end)
    const item = `   <rdf:li rdf:parseType="Resource">\n${indent}      <xmpDM:trackName>CuePoint Markers</xmpDM:trackName>\n${indent}      <xmpDM:trackType>Cue</xmpDM:trackType>\n${indent}      <xmpDM:frameRate>f${sampleRate}</xmpDM:frameRate>\n${indent}      ${xmpMarkers(markers, `${indent}      `)}\n${indent}   </rdf:li>\n${indent}`
    return xml.slice(0, bag.inner.end) + item + xml.slice(bag.inner.end)
  }
  if (tracks) return null
  // No Tracks at all: add them to the first description, declaring the namespace if needed.
  const description = /<rdf:Description\b[^>]*?>/.exec(xml)
  if (!description || description[0].endsWith('/>')) return null
  const close = xml.indexOf('</rdf:Description>', description.index)
  if (close < 0) return null
  const indent = indentBefore(xml, close)
  const block = `   <xmpDM:Tracks>\n${indent}      <rdf:Bag>\n${indent}         <rdf:li rdf:parseType="Resource">\n${indent}            <xmpDM:trackName>CuePoint Markers</xmpDM:trackName>\n${indent}            <xmpDM:trackType>Cue</xmpDM:trackType>\n${indent}            <xmpDM:frameRate>f${sampleRate}</xmpDM:frameRate>\n${indent}            ${xmpMarkers(markers, `${indent}            `)}\n${indent}         </rdf:li>\n${indent}      </rdf:Bag>\n${indent}   </xmpDM:Tracks>\n${indent}`
  let output = xml.slice(0, close) + block + xml.slice(close)
  if (!description[0].includes('xmlns:xmpDM=')) {
    const at = description.index + '<rdf:Description'.length
    output = `${output.slice(0, at)} xmlns:xmpDM="${XMP_DM}"${output.slice(at)}`
  }
  return output
}

// ---------------------------------------------------------------------------------------------

/** The markers stored in a WAV's metadata chunks, in time order. */
export function readMarkers(layout: WavLayout, sampleRate: number): MarkerData[] {
  const xmp = layout.chunks.find((chunk) => chunk.id === '_PMX')?.body
  if (xmp) {
    const fromXmp = readXmpMarkers(new TextDecoder().decode(xmp), sampleRate)
    if (fromXmp) return fromXmp
  }
  return readChunkMarkers(layout.chunks)
}

/**
 * A copy of the layout whose chunks carry `markers` instead of whatever it held: new `cue ` and
 * `LIST`/`adtl` chunks where the old ones were (or at the end), and the XMP packet's markers
 * rewritten. Every other chunk is untouched.
 */
export function withMarkers(
  layout: WavLayout,
  markers: MarkerData[],
  sampleRate: number,
): WavLayout {
  const sorted = sortMarkers(markers)
  const chunks: WavChunk[] = []
  let placed = false
  const markerChunks = (): WavChunk[] =>
    sorted.length
      ? [
          { id: 'cue ', body: cueChunk(sorted) },
          { id: 'LIST', body: adtlChunk(sorted) },
        ]
      : []
  for (const chunk of layout.chunks) {
    if (chunk.id === 'cue ' || isAdtl(chunk)) {
      if (!placed) chunks.push(...markerChunks())
      placed = true
    } else if (chunk.id === '_PMX' && chunk.body) {
      const xml = new TextDecoder().decode(chunk.body)
      const rewritten = rewriteXmpMarkers(xml, sorted, sampleRate)
      chunks.push(rewritten === null ? chunk : { id: '_PMX', body: encoder.encode(rewritten) })
    } else {
      chunks.push(chunk)
    }
  }
  if (!placed) chunks.push(...markerChunks())
  return { ...layout, chunks }
}

/** `Marker 01`, `Marker 02`, …: the next free number after the highest one in use. */
export function nextMarkerName(markers: MarkerData[]) {
  let highest = 0
  for (const marker of markers) {
    const match = /^Marker (\d+)$/.exec(marker.name)
    if (match) highest = Math.max(highest, Number(match[1]))
  }
  return `Marker ${String(highest + 1).padStart(2, '0')}`
}

/** Lowercase letters and digits only, as the VoW file name rule allows in each part. */
export function namePart(text: string) {
  return text
    .normalize('NFKD')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
}

/** `quest-npc-N.wav` for each range, numbered in time order from `first`. */
export function exportNames(count: number, quest: string, character: string, first: number) {
  const prefix = `${namePart(quest)}-${namePart(character)}`
  return Array.from({ length: count }, (_, index) => `${prefix}-${first + index}.wav`)
}
