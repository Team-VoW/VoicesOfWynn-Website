import type { InjectionKey } from 'vue'
import type { Marker } from '../lib/markers'

/** What a marker's menu asks of the editor around it, beyond changing the markers themselves. */
export interface MarkerViewActions {
  /** Plays the marker's range, or from a point marker on. */
  play: (marker: Marker) => void
  /** Scrolls (and for a long range zooms) the waveform to the marker. */
  reveal: (marker: Marker, zoom?: boolean) => void
  /** Renames it in place in the Markers panel. */
  rename: (marker: Marker) => void
  /** Opens its properties: name, description and exact times. */
  edit: (marker: Marker) => void
}

export const MARKER_ACTIONS: InjectionKey<MarkerViewActions> = Symbol('marker actions')
