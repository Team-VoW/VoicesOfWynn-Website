<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref } from 'vue'
import { fft } from './fft'

const props = defineProps<{
  loadAudio: () => Promise<Blob>
  position: number
  duration: number
}>()
const emit = defineEmits<{ seek: [fraction: number] }>()
const waveform = ref<HTMLCanvasElement | null>(null)
const spectrum = ref<HTMLCanvasElement | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const abort = new AbortController()
const playhead = computed(() =>
  props.duration > 0 ? Math.min(100, Math.max(0, (props.position / props.duration) * 100)) : 0,
)

function formatTime(seconds: number) {
  const whole = Math.max(0, Math.floor(seconds))
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

function seekAt(event: PointerEvent) {
  const target = event.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  emit('seek', Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)))
}

function beginSeek(event: PointerEvent) {
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  seekAt(event)
}

function dragSeek(event: PointerEvent) {
  if (event.buttons & 1) seekAt(event)
}

function seekBy(seconds: number) {
  if (props.duration > 0)
    emit('seek', Math.min(1, Math.max(0, (props.position + seconds) / props.duration)))
}

const colors = [
  [12, 18, 37],
  [28, 47, 90],
  [37, 93, 146],
  [58, 158, 189],
  [154, 214, 203],
  [255, 220, 139],
] as const

function colorAt(intensity: number) {
  const scaled = Math.min(colors.length - 1, intensity * (colors.length - 1))
  const index = Math.min(colors.length - 2, Math.floor(scaled))
  const fraction = scaled - index
  return [0, 1, 2].map((channel) =>
    Math.round(colors[index]![channel]! * (1 - fraction) + colors[index + 1]![channel]! * fraction),
  )
}

function draw(samples: Float32Array, sampleRate: number) {
  const wave = waveform.value?.getContext('2d')
  const spectral = spectrum.value?.getContext('2d')
  if (!wave || !spectral || !waveform.value || !spectrum.value) return

  const width = waveform.value.width
  const waveHeight = waveform.value.height
  wave.clearRect(0, 0, width, waveHeight)
  wave.fillStyle = '#a340c4'
  for (let x = 0; x < width; x++) {
    const start = Math.floor((x * samples.length) / width)
    const end = Math.min(
      samples.length,
      Math.max(start + 1, Math.floor(((x + 1) * samples.length) / width)),
    )
    let min = 1,
      max = -1
    const stride = Math.max(1, Math.floor((end - start) / 512))
    for (let i = start; i < end; i += stride) {
      min = Math.min(min, samples[i]!)
      max = Math.max(max, samples[i]!)
    }
    wave.fillRect(x, ((1 - max) * waveHeight) / 2, 1, Math.max(1, ((max - min) * waveHeight) / 2))
  }

  const height = spectrum.value.height
  const fftSize = 2048
  const real = new Float32Array(fftSize)
  const imaginary = new Float32Array(fftSize)
  const window = Float32Array.from(
    { length: fftSize },
    (_, i) => 0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (fftSize - 1)),
  )
  const image = spectral.createImageData(width, height)
  const maxFrequency = Math.min(9000, sampleRate / 2)
  const minFrequency = 70
  for (let x = 0; x < width; x++) {
    const center = Math.floor(((x + 0.5) * samples.length) / width)
    for (let i = 0; i < fftSize; i++) {
      const index = center + i - fftSize / 2
      real[i] = (samples[index] ?? 0) * window[i]!
      imaginary[i] = 0
    }
    fft(real, imaginary)
    for (let y = 0; y < height; y++) {
      const frequency = minFrequency * Math.pow(maxFrequency / minFrequency, 1 - y / (height - 1))
      const exactBin = Math.min(fftSize / 2 - 2, Math.max(1, (frequency * fftSize) / sampleRate))
      const bin = Math.floor(exactBin)
      const blend = exactBin - bin
      const low = Math.hypot(real[bin]!, imaginary[bin]!)
      const high = Math.hypot(real[bin + 1]!, imaginary[bin + 1]!)
      const magnitude = (low * (1 - blend) + high * blend) / (fftSize * 0.25)
      const decibels = 20 * Math.log10(magnitude + 1e-8)
      const intensity = Math.pow(Math.min(1, Math.max(0, (decibels + 76) / 64)), 1.3)
      const [red, green, blue] = colorAt(intensity)
      const pixel = (y * width + x) * 4
      image.data[pixel] = red!
      image.data[pixel + 1] = green!
      image.data[pixel + 2] = blue!
      image.data[pixel + 3] = 255
    }
  }
  spectral.putImageData(image, 0, 0)
}

onMounted(async () => {
  let context: AudioContext | undefined
  try {
    const bytes = await (await props.loadAudio()).arrayBuffer()
    if (abort.signal.aborted) return
    context = new AudioContext()
    const decoded = await context.decodeAudioData(bytes)
    if (abort.signal.aborted) return
    const samples = decoded.getChannelData(0)
    draw(samples, decoded.sampleRate)
    status.value = 'ready'
  } catch {
    if (!abort.signal.aborted) status.value = 'error'
  } finally {
    void context?.close()
  }
})

onBeforeUnmount(() => abort.abort())
</script>

<template>
  <div class="space-y-2">
    <p v-if="status === 'loading'" class="text-sm text-muted-foreground">Analyzing audio…</p>
    <p v-if="status === 'error'" class="text-sm text-muted-foreground">
      The audio visualization could not be loaded. You can still listen or download the recording.
    </p>
    <div :class="status === 'ready' ? '' : 'hidden'">
      <div class="mb-1 flex items-center justify-between text-xs text-muted-foreground">
        <span class="font-medium">Waveform</span>
        <span class="tabular-nums">{{ formatTime(position) }} / {{ formatTime(duration) }}</span>
      </div>
      <div
        role="slider"
        tabindex="0"
        class="relative cursor-crosshair overflow-hidden rounded-md border bg-primary/[0.04] touch-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        aria-label="Seek in waveform"
        :aria-valuemin="0"
        :aria-valuemax="Math.round(duration)"
        :aria-valuenow="Math.round(position)"
        :aria-valuetext="`${formatTime(position)} of ${formatTime(duration)}`"
        @pointerdown="beginSeek"
        @pointermove="dragSeek"
        @keydown.right.prevent="seekBy(5)"
        @keydown.left.prevent="seekBy(-5)"
      >
        <canvas ref="waveform" width="720" height="88" class="block w-full" aria-hidden="true" />
        <span
          class="pointer-events-none absolute inset-y-0 left-0 bg-primary/8"
          :style="{ width: `${playhead}%` }"
        />
        <span
          class="pointer-events-none absolute inset-y-0 w-0.5 bg-primary shadow-[0_0_0_1px_white]"
          :style="{ left: `${playhead}%` }"
        />
      </div>
      <p class="mb-1 mt-3 text-xs font-medium text-muted-foreground">Spectrogram</p>
      <div
        role="slider"
        tabindex="0"
        class="relative cursor-crosshair overflow-hidden rounded-md border bg-[#0c1225] touch-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        aria-label="Seek in spectrogram"
        :aria-valuemin="0"
        :aria-valuemax="Math.round(duration)"
        :aria-valuenow="Math.round(position)"
        :aria-valuetext="`${formatTime(position)} of ${formatTime(duration)}`"
        @pointerdown="beginSeek"
        @pointermove="dragSeek"
        @keydown.right.prevent="seekBy(5)"
        @keydown.left.prevent="seekBy(-5)"
      >
        <canvas ref="spectrum" width="720" height="160" class="block w-full" aria-hidden="true" />
        <span
          class="pointer-events-none absolute inset-y-0 w-0.5 bg-white shadow-[0_0_4px_1px_#0c1225]"
          :style="{ left: `${playhead}%` }"
        />
      </div>
      <div
        class="mt-1 flex justify-between text-[10px] tabular-nums text-muted-foreground"
        aria-hidden="true"
      >
        <span v-for="tick in [0, 0.25, 0.5, 0.75, 1]" :key="tick">{{
          formatTime(duration * tick)
        }}</span>
      </div>
    </div>
  </div>
</template>
