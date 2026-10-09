/** `m:ss.fff`, or `h:mm:ss.fff` from an hour up, with as many fraction digits as asked for. */
export function formatTime(seconds: number, decimals = 3) {
  const scale = 10 ** decimals
  // Rounded first, so 59.9996 s reads 1:00.000 rather than 0:60.000.
  const safe = Number.isFinite(seconds) ? Math.round(Math.max(0, seconds) * scale) / scale : 0
  const hours = Math.floor(safe / 3600)
  const minutes = Math.floor((safe - hours * 3600) / 60)
  const rest = safe - hours * 3600 - minutes * 60
  const [whole, fraction] = rest.toFixed(decimals).split('.')
  const clock = hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}` : `${minutes}`
  return `${clock}:${whole!.padStart(2, '0')}${fraction ? `.${fraction}` : ''}`
}

/**
 * Seconds from `h:mm:ss.fff`, `m:ss.fff` or plain seconds, as typed into a time field. Null when it
 * is not a time.
 */
export function parseTime(text: string) {
  const parts = text.trim().split(':')
  if (parts.length > 3 || parts.some((part) => !/^\d+(\.\d*)?$/.test(part))) return null
  // Only the last part may have a fraction.
  if (parts.slice(0, -1).some((part) => part.includes('.'))) return null
  return parts.reduce((total, part) => total * 60 + Number(part), 0)
}

export function formatDb(value: number | null | undefined, unit: string, decimals = 1) {
  if (value === null || value === undefined || Number.isNaN(value)) return '—'
  if (!Number.isFinite(value)) return `−∞ ${unit}`
  return `${value.toFixed(decimals).replace('-', '−')} ${unit}`
}

export function formatSignedDb(value: number) {
  if (!Number.isFinite(value)) return '—'
  const rounded = Math.round(value * 10) / 10
  if (rounded === 0) return '0.0 dB'
  return `${rounded > 0 ? '+' : '−'}${Math.abs(rounded).toFixed(1)} dB`
}
