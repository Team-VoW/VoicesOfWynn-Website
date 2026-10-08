const TAPS_PER_SIDE = 32

function blackman(x: number) {
  return 0.42 + 0.5 * Math.cos(Math.PI * x) + 0.08 * Math.cos(2 * Math.PI * x)
}

/**
 * Band-limited sample rate conversion with a Blackman-windowed sinc. Downsampling lowers the
 * cutoff to the new Nyquist so nothing above it folds back as aliasing.
 */
export function resample(input: Float32Array, from: number, to: number): Float32Array {
  if (from === to || input.length === 0) return input.slice()
  const ratio = to / from
  const output = new Float32Array(Math.max(1, Math.round(input.length * ratio)))
  const cutoff = Math.min(1, ratio)
  const radius = Math.ceil(TAPS_PER_SIDE / cutoff)
  for (let i = 0; i < output.length; i++) {
    const center = i / ratio
    const first = Math.max(0, Math.ceil(center - radius))
    const last = Math.min(input.length - 1, Math.floor(center + radius))
    let sum = 0
    for (let j = first; j <= last; j++) {
      const distance = center - j
      const x = Math.PI * cutoff * distance
      const sinc = x === 0 ? 1 : Math.sin(x) / x
      sum += input[j]! * cutoff * sinc * blackman(distance / radius)
    }
    output[i] = sum
  }
  return output
}
