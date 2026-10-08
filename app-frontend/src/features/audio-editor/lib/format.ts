/** `m:ss.fff`, with as many fraction digits as asked for. */
export function formatTime(seconds: number, decimals = 3) {
  const safe = Number.isFinite(seconds) ? Math.max(0, seconds) : 0
  const minutes = Math.floor(safe / 60)
  const rest = safe - minutes * 60
  const [whole, fraction] = rest.toFixed(decimals).split('.')
  return `${minutes}:${whole!.padStart(2, '0')}${fraction ? `.${fraction}` : ''}`
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
