// Minimal store-only ZIP writer for batch export. WAV barely compresses with deflate, so storing
// keeps this to a CRC and two headers instead of pulling in a compression library.

const CRC_TABLE = (() => {
  const table = new Uint32Array(256)
  for (let n = 0; n < 256; n++) {
    let c = n
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    table[n] = c >>> 0
  }
  return table
})()

export function crc32(bytes: Uint8Array) {
  let crc = 0xffffffff
  for (let i = 0; i < bytes.length; i++) crc = CRC_TABLE[(crc ^ bytes[i]!) & 0xff]! ^ (crc >>> 8)
  return (crc ^ 0xffffffff) >>> 0
}

export interface ZipEntry {
  name: string
  data: Uint8Array
}

export function createZip(entries: ZipEntry[]) {
  const encoder = new TextEncoder()
  const local: Uint8Array[] = []
  const central: Uint8Array[] = []
  let offset = 0

  for (const entry of entries) {
    const name = encoder.encode(entry.name)
    const crc = crc32(entry.data)
    const size = entry.data.length

    const header = new Uint8Array(30 + name.length)
    const h = new DataView(header.buffer)
    h.setUint32(0, 0x04034b50, true)
    h.setUint16(4, 20, true)
    h.setUint16(6, 0x0800, true) // UTF-8 names
    h.setUint32(14, crc, true)
    h.setUint32(18, size, true)
    h.setUint32(22, size, true)
    h.setUint16(26, name.length, true)
    header.set(name, 30)
    local.push(header, entry.data)

    const record = new Uint8Array(46 + name.length)
    const r = new DataView(record.buffer)
    r.setUint32(0, 0x02014b50, true)
    r.setUint16(4, 20, true)
    r.setUint16(6, 20, true)
    r.setUint16(8, 0x0800, true)
    r.setUint32(16, crc, true)
    r.setUint32(20, size, true)
    r.setUint32(24, size, true)
    r.setUint16(28, name.length, true)
    r.setUint32(42, offset, true)
    record.set(name, 46)
    central.push(record)

    offset += header.length + size
  }

  const centralSize = central.reduce((sum, part) => sum + part.length, 0)
  const end = new Uint8Array(22)
  const e = new DataView(end.buffer)
  e.setUint32(0, 0x06054b50, true)
  e.setUint16(8, entries.length, true)
  e.setUint16(10, entries.length, true)
  e.setUint32(12, centralSize, true)
  e.setUint32(16, offset, true)

  return new Blob(
    [...local, ...central, end].map((part) => part as BlobPart),
    {
      type: 'application/zip',
    },
  )
}
