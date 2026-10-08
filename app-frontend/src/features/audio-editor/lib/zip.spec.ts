import { describe, expect, it } from 'vitest'
import { crc32, createZip } from './zip'

describe('zip', () => {
  it('computes the standard CRC-32 check value', () => {
    expect(crc32(new TextEncoder().encode('123456789'))).toBe(0xcbf43926)
  })

  it('stores entries with matching local and central records', async () => {
    const data = new TextEncoder().encode('hello')
    const zip = new Uint8Array(await createZip([{ name: 'a/b.wav', data }]).arrayBuffer())
    const view = new DataView(zip.buffer)
    const end = zip.length - 22

    expect(view.getUint32(0, true)).toBe(0x04034b50)
    expect(view.getUint32(end, true)).toBe(0x06054b50)
    expect(view.getUint16(end + 10, true)).toBe(1)
    expect(new TextDecoder().decode(zip.subarray(30, 37))).toBe('a/b.wav')
    expect(new TextDecoder().decode(zip.subarray(37, 42))).toBe('hello')
  })
})
