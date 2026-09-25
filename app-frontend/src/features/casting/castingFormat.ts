import type { CastingRoundStatus, CastingSource } from '@/api/types'

export function statusLabel(status: CastingRoundStatus) {
  return { Draft: 'Draft', Open: 'Open', Closed: 'Closed', Archived: 'Archived' }[status]
}

/** Badge colours per status; draft and archived recede, open stands out. */
export function statusClass(status: CastingRoundStatus) {
  return {
    Draft: 'bg-muted text-muted-foreground',
    Open: 'bg-emerald-50 text-emerald-800',
    Closed: 'bg-primary/10 text-primary',
    Archived: 'bg-muted text-muted-foreground/80',
  }[status]
}

export function sourceLabel(source: CastingSource) {
  return { Manual: 'Manual', Ccc: 'Casting Call Club', Discord: 'Discord' }[source]
}

export function formatDate(iso: string | null) {
  return iso ? new Date(iso).toLocaleDateString(undefined, { dateStyle: 'medium' }) : null
}

/** `datetime-local` has no timezone, and the API stores UTC - convert on both edges. */
export function toLocalInput(iso: string | null) {
  if (!iso) return ''
  const date = new Date(iso)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}

export function toIso(local: string) {
  return local ? new Date(local).toISOString() : null
}
