<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { Download, Pause, Play, SlidersHorizontal } from 'lucide-vue-next'
import { useAudioPlayback } from './useAudioPlayback'
import AudioAnalysis from './AudioAnalysis.vue'
import { apiFetchBlob } from '@/api/client'

/**
 * A play button with a clickable progress bar, for listening through auditions where jumping to
 * the last line matters. Shares the page-wide "one clip at a time" rule with AudioPlayer.
 */
const props = withDefaults(
  defineProps<{
    src: string
    label: string
    /** Known up front from the API so the time reads correctly before anything is downloaded. */
    durationSeconds?: number | null
    /** Only the round button, for dense lists. */
    compact?: boolean
    /** Show the detailed listening controls for casting votes. */
    expandable?: boolean
    audioFilePath?: string
  }>(),
  { durationSeconds: null, compact: false, expandable: false },
)

const { claim, release } = useAudioPlayback()
const audio = ref<HTMLAudioElement | null>(null)
const playing = ref(false)
const failed = ref(false)
const position = ref(0)
const loadedDuration = ref<number | null>(null)
const expanded = ref(false)
const volume = ref(100)
const loadingAudio = ref(false)
const boostFailed = ref(false)
const downloading = ref(false)
const downloadFailed = ref(false)
let audioBlobPromise: Promise<Blob> | null = null
let playbackUrl: string | null = null
let audioContext: AudioContext | null = null
let gainNode: GainNode | null = null
let boostPromise: Promise<void> | null = null
const emit = defineEmits<{ expanded: [value: boolean] }>()

function loadAudio() {
  if (!props.audioFilePath) return Promise.reject(new Error('No audio file path'))
  audioBlobPromise ??= apiFetchBlob(props.audioFilePath).catch((error: unknown) => {
    audioBlobPromise = null
    throw error
  })
  return audioBlobPromise
}

async function enableBoost() {
  if (boostPromise) return boostPromise
  boostPromise = prepareEnhancedPlayback().finally(() => {
    boostPromise = null
  })
  return boostPromise
}

async function prepareEnhancedPlayback() {
  const element = audio.value
  if (!element || !props.audioFilePath || gainNode) return
  // A blob URL lets Web Audio amplify the clip without cross-origin media restrictions.
  // Resume during the volume gesture, before the asynchronous file download.
  audioContext ??= new AudioContext()
  void audioContext.resume().catch(() => {})
  loadingAudio.value = true
  boostFailed.value = false
  try {
    const blob = await loadAudio()
    if (!audio.value) return
    const wasPlaying = !element.paused
    const currentTime = element.currentTime
    element.pause()
    playbackUrl = URL.createObjectURL(blob)
    element.src = playbackUrl
    element.load()
    if (element.readyState < HTMLMediaElement.HAVE_METADATA) {
      await new Promise<void>((resolve, reject) => {
        element.addEventListener('loadedmetadata', () => resolve(), { once: true })
        element.addEventListener('error', () => reject(new Error('Audio could not load')), {
          once: true,
        })
      })
    }
    element.currentTime = currentTime
    gainNode = audioContext.createGain()
    audioContext.createMediaElementSource(element).connect(gainNode)
    gainNode.connect(audioContext.destination)
    element.volume = 1
    gainNode.gain.value = volume.value / 100
    await audioContext.resume()
    if (wasPlaying) {
      claim(element)
      await element.play()
    }
  } catch {
    boostFailed.value = true
    volume.value = 100
    element.volume = 1
  } finally {
    loadingAudio.value = false
  }
}

function toggleExpanded() {
  expanded.value = !expanded.value
  emit('expanded', expanded.value)
}

const duration = computed(() => loadedDuration.value ?? props.durationSeconds ?? 0)
const percent = computed(() =>
  duration.value > 0 ? Math.min(100, (position.value / duration.value) * 100) : 0,
)

function format(seconds: number) {
  const whole = Math.max(0, Math.floor(seconds))
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

async function start() {
  const element = audio.value
  if (!element) return
  if (loadingAudio.value) return
  claim(element)
  try {
    await element.play()
  } catch {
    // Claimed away by another clip before playback began; the error event marks real failures.
    playing.value = false
  }
}

function toggle() {
  if (playing.value) audio.value?.pause()
  else void start()
}

function seek(event: MouseEvent) {
  if (duration.value <= 0) return
  const bar = event.currentTarget as HTMLElement
  const rect = bar.getBoundingClientRect()
  seekToFraction((event.clientX - rect.left) / rect.width)
}

function seekToFraction(fraction: number) {
  const element = audio.value
  if (!element || duration.value <= 0) return
  const time = Math.min(duration.value, Math.max(0, fraction * duration.value))
  position.value = time
  element.currentTime = time
  if (!playing.value) void start()
}

function seekBy(seconds: number) {
  if (duration.value > 0) seekToFraction((position.value + seconds) / duration.value)
}

function setVolume(event: Event) {
  volume.value = Number((event.target as HTMLInputElement).value)
  if (gainNode) gainNode.gain.value = volume.value / 100
  else if (volume.value <= 100 && audio.value) audio.value.volume = volume.value / 100
  else if (volume.value > 100) void enableBoost()
}

async function download() {
  downloading.value = true
  downloadFailed.value = false
  try {
    const blobUrl = URL.createObjectURL(await loadAudio())
    const link = document.createElement('a')
    link.href = blobUrl
    link.download = `${props.label.replace(/[^a-z0-9_-]+/gi, '-').replace(/^-|-$/g, '') || 'audition'}.mp3`
    document.body.append(link)
    link.click()
    link.remove()
    setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000)
  } catch {
    downloadFailed.value = true
  } finally {
    downloading.value = false
  }
}

function onStop() {
  playing.value = false
  if (audio.value) release(audio.value)
}

function onEnded() {
  onStop()
  position.value = 0
}

onBeforeUnmount(() => {
  if (audio.value) {
    audio.value.pause()
    release(audio.value)
  }
  if (playbackUrl) URL.revokeObjectURL(playbackUrl)
  void audioContext?.close()
})
</script>

<template>
  <div class="min-w-0">
    <div class="flex min-w-0 items-center gap-3">
      <button
        type="button"
        class="flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-full border transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary disabled:cursor-not-allowed disabled:opacity-50"
        :class="
          playing
            ? 'border-primary bg-primary text-primary-foreground'
            : 'border-primary/25 bg-background text-primary hover:bg-primary/5'
        "
        :aria-label="playing ? `Pause ${label}` : `Play ${label}`"
        :aria-pressed="playing"
        :title="failed ? 'This audition could not be played' : label"
        :disabled="failed || loadingAudio"
        @click="toggle"
      >
        <Pause v-if="playing" class="size-4" aria-hidden="true" />
        <Play v-else class="size-4 translate-x-px" aria-hidden="true" />
      </button>

      <template v-if="!compact">
        <div
          role="slider"
          tabindex="0"
          class="flex h-5 min-w-16 flex-1 cursor-pointer items-center focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
          :aria-label="`Seek in ${label}`"
          :aria-valuemin="0"
          :aria-valuemax="Math.round(duration)"
          :aria-valuenow="Math.round(position)"
          :aria-valuetext="`${format(position)} of ${format(duration)}`"
          @click="seek"
          @keydown.right.prevent="seekBy(5)"
          @keydown.left.prevent="seekBy(-5)"
        >
          <div class="h-1.5 w-full overflow-hidden rounded-full bg-primary/10">
            <div class="h-full rounded-full bg-primary/60" :style="{ width: `${percent}%` }" />
          </div>
        </div>
        <span class="shrink-0 text-xs text-muted-foreground tabular-nums">
          {{ format(position) }} / {{ format(duration) }}
        </span>
      </template>

      <button
        v-if="expandable"
        type="button"
        class="inline-flex shrink-0 cursor-pointer items-center gap-1 text-xs font-medium text-primary hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
        :aria-expanded="expanded"
        @click="toggleExpanded"
      >
        <SlidersHorizontal class="size-3.5" aria-hidden="true" />
        {{ expanded ? 'Close' : 'View more' }}
      </button>
    </div>

    <div
      v-if="expanded"
      class="mt-3 space-y-4 rounded-md border border-primary/15 bg-background p-4"
    >
      <AudioAnalysis
        v-if="audioFilePath"
        :load-audio="loadAudio"
        :position="position"
        :duration="duration"
        @seek="seekToFraction"
      />
      <div class="flex flex-wrap items-center gap-x-5 gap-y-3 border-t pt-3">
        <label class="flex min-w-44 flex-1 items-center gap-2 text-sm">
          <span class="shrink-0">Volume</span>
          <input
            type="range"
            min="0"
            max="200"
            :value="volume"
            :aria-label="`Volume for ${label}`"
            class="min-w-20 flex-1 accent-primary"
            @input="setVolume"
          />
          <span class="w-9 text-right text-xs tabular-nums text-muted-foreground"
            >{{ volume }}%</span
          >
        </label>
        <button
          type="button"
          class="inline-flex cursor-pointer items-center gap-1.5 text-sm font-medium text-primary hover:underline disabled:opacity-50"
          :disabled="downloading"
          @click="download"
        >
          <Download class="size-4" aria-hidden="true" />
          {{ downloading ? 'Downloading…' : 'Download audio' }}
        </button>
      </div>
      <p v-if="downloadFailed" role="alert" class="text-sm text-destructive">
        The download failed. Please try again.
      </p>
      <p v-if="boostFailed" role="alert" class="text-sm text-destructive">
        Amplification could not be loaded. Normal volume is still available.
      </p>
    </div>

    <audio
      ref="audio"
      :src="src"
      preload="none"
      class="sr-only"
      @play="
        () => {
          playing = true
          failed = false
        }
      "
      @pause="onStop"
      @ended="onEnded"
      @timeupdate="position = ($event.target as HTMLAudioElement).currentTime"
      @loadedmetadata="
        loadedDuration = Number.isFinite(($event.target as HTMLAudioElement).duration)
          ? ($event.target as HTMLAudioElement).duration
          : null
      "
      @error="failed = true"
    />
  </div>
</template>
