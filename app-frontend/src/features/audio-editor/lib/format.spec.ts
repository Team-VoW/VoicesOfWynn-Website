import { describe, expect, it } from 'vitest'
import { formatTime, parseTime } from './format'

describe('formatTime', () => {
  it('formats minutes, seconds and the asked-for fraction', () => {
    expect(formatTime(83.25)).toBe('1:23.250')
    expect(formatTime(5.5, 1)).toBe('0:05.5')
  })

  it('carries a value that rounds up into the next minute', () => {
    expect(formatTime(59.9996)).toBe('1:00.000')
    expect(formatTime(119.96, 1)).toBe('2:00.0')
  })

  it('reads back what it writes, and plain seconds', () => {
    expect(parseTime('2:00:41.974')).toBeCloseTo(7241.974)
    expect(parseTime('1:23.250')).toBeCloseTo(83.25)
    expect(parseTime(' 12.5 ')).toBe(12.5)
    expect(parseTime('1.5:00')).toBeNull()
    expect(parseTime('abc')).toBeNull()
    expect(parseTime('')).toBeNull()
  })

  it('adds hours from an hour up', () => {
    expect(formatTime(7241.974)).toBe('2:00:41.974')
    expect(formatTime(3599.9996)).toBe('1:00:00.000')
    expect(formatTime(3725, 0)).toBe('1:02:05')
  })
})
