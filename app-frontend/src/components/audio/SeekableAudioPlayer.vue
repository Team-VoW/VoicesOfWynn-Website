<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { Pause, Play } from 'lucide-vue-next'
import { useAudioPlayback } from './useAudioPlayback'

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
  }>(),
  { durationSeconds: null, compact: false },
)

const { claim, release } = useAudioPlayback()
const audio = ref<HTMLAudioElement | null>(null)
const playing = ref(false)
const failed = ref(false)
const position = ref(0)
const loadedDuration = ref<number | null>(null)

const duration = computed(() => loadedDuration.value ?? props.durationSeconds ?? 0)
const percent = computed(() => (duration.value > 0 ? Math.min(100, (position.value / duration.value) * 100) : 0))

function format(seconds: number) {
  const whole = Math.max(0, Math.floor(seconds))
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

async function start() {
  const element = audio.value
  if (!element) return
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
  const element = audio.value
  if (!element || duration.value <= 0) return
  const bar = event.currentTarget as HTMLElement
  const rect = bar.getBoundingClientRect()
  const fraction = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width))
  element.currentTime = fraction * duration.value
  position.value = element.currentTime
  if (!playing.value) void start()
}

function seekBy(seconds: number) {
  const element = audio.value
  if (!element || duration.value <= 0) return
  element.currentTime = Math.min(duration.value, Math.max(0, element.currentTime + seconds))
  position.value = element.currentTime
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
})
</script>

<template>
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
      :disabled="failed"
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
