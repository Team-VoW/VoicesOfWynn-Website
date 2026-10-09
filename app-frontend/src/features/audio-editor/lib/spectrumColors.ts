// Colour maps for the spectral display, each a lookup of 256 RGB triples from silent to loudest.

export type SpectrumColors = 'ocean' | 'fire' | 'gray'

export const SPECTRUM_COLOR_NAMES: Record<SpectrumColors, string> = {
  ocean: 'Ocean',
  fire: 'Fire',
  gray: 'Grayscale',
}

const STOPS: Record<SpectrumColors, readonly (readonly [number, number, number])[]> = {
  ocean: [
    [12, 18, 37],
    [28, 47, 90],
    [37, 93, 146],
    [58, 158, 189],
    [154, 214, 203],
    [255, 220, 139],
  ],
  // Like Audition's and RX's: black through violet and red to yellow-white.
  fire: [
    [4, 2, 10],
    [42, 10, 74],
    [120, 20, 110],
    [205, 45, 70],
    [248, 130, 30],
    [252, 220, 90],
    [255, 255, 230],
  ],
  gray: [
    [6, 6, 8],
    [255, 255, 255],
  ],
}

function build(stops: readonly (readonly [number, number, number])[]) {
  const table = new Uint8ClampedArray(256 * 3)
  for (let value = 0; value < 256; value++) {
    const scaled = (value / 255) * (stops.length - 1)
    const index = Math.min(stops.length - 2, Math.floor(scaled))
    const fraction = scaled - index
    for (let c = 0; c < 3; c++)
      table[value * 3 + c] = stops[index]![c]! * (1 - fraction) + stops[index + 1]![c]! * fraction
  }
  return table
}

const tables = new Map<SpectrumColors, Uint8ClampedArray>()

/** The colour map as 256 packed RGB triples. */
export function spectrumTable(colors: SpectrumColors) {
  let table = tables.get(colors)
  if (!table) {
    table = build(STOPS[colors] ?? STOPS.ocean)
    tables.set(colors, table)
  }
  return table
}

/** The colour drawn for silence, for the area behind a spectrogram still being built. */
export function spectrumBackground(colors: SpectrumColors) {
  const [red, green, blue] = (STOPS[colors] ?? STOPS.ocean)[0]!
  return `rgb(${red}, ${green}, ${blue})`
}
