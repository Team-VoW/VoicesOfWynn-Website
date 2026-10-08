/** In-place iterative radix-2 FFT. `real.length` must be a power of two. */
export function fft(real: Float32Array, imaginary: Float32Array) {
  const size = real.length
  for (let i = 1, j = 0; i < size; i++) {
    let bit = size >> 1
    for (; j & bit; bit >>= 1) j ^= bit
    j ^= bit
    if (i < j) {
      ;[real[i], real[j]] = [real[j]!, real[i]!]
      ;[imaginary[i], imaginary[j]] = [imaginary[j]!, imaginary[i]!]
    }
  }
  for (let length = 2; length <= size; length <<= 1) {
    const angle = (-2 * Math.PI) / length
    const stepReal = Math.cos(angle)
    const stepImaginary = Math.sin(angle)
    for (let base = 0; base < size; base += length) {
      let twiddleReal = 1
      let twiddleImaginary = 0
      for (let offset = 0; offset < length / 2; offset++) {
        const left = base + offset
        const right = left + length / 2
        const r = real[right]! * twiddleReal - imaginary[right]! * twiddleImaginary
        const im = real[right]! * twiddleImaginary + imaginary[right]! * twiddleReal
        real[right] = real[left]! - r
        imaginary[right] = imaginary[left]! - im
        real[left] = real[left]! + r
        imaginary[left] = imaginary[left]! + im
        const nextReal = twiddleReal * stepReal - twiddleImaginary * stepImaginary
        twiddleImaginary = twiddleReal * stepImaginary + twiddleImaginary * stepReal
        twiddleReal = nextReal
      }
    }
  }
}
