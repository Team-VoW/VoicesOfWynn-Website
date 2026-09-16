<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { Pause, Play } from 'lucide-vue-next'
import { useAudioPlayback } from './useAudioPlayback'

const props = defineProps<{ src: string; label: string }>()

const { claim, release } = useAudioPlayback()
const audio = ref<HTMLAudioElement | null>(null)
const playing = ref(false)
const failed = ref(false)

// The bar heights are fixed rather than sampled from the audio: decoding every clip to draw a
// real waveform would download all of them, which is exactly what lazy loading avoids.
const bars = [0.35, 0.6, 0.85, 1, 0.7, 0.45, 0.8, 1, 0.55, 0.3, 0.65, 0.9, 0.5, 0.75, 0.4]

const title = computed(() => (failed.value ? 'This recording could not be played' : props.label))

async function toggle() {
  const element = audio.value
  if (!element) return

  if (playing.value) {
    element.pause()
    return
  }

  claim(element)
  try {
    await element.play()
  } catch {
    // Pausing an element before playback has begun rejects this promise, which is exactly what
    // claiming the player for another clip does - so a rejection is no proof the clip is broken.
    // A recording that genuinely cannot be played raises the element's error event instead.
    playing.value = false
  }
}

function onPlay() {
  playing.value = true
  failed.value = false
}

function onStop() {
  playing.value = false
  if (audio.value) release(audio.value)
}

onBeforeUnmount(() => {
  if (audio.value) {
    audio.value.pause()
    release(audio.value)
  }
})
</script>

<template>
  <div class="flex items-center gap-3">
    <button
      type="button"
      class="flex size-9 shrink-0 cursor-pointer items-center justify-center rounded-full bg-[#a340c4]/12 text-[#7b1a9b] transition-colors hover:bg-[#a340c4]/22 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4] disabled:cursor-not-allowed disabled:opacity-50"
      :aria-label="playing ? `Pause ${label}` : `Play ${label}`"
      :aria-pressed="playing"
      :title="title"
      :disabled="failed"
      @click="toggle"
    >
      <Pause v-if="playing" class="size-4" aria-hidden="true" />
      <Play v-else class="size-4 translate-x-px" aria-hidden="true" />
    </button>

    <div class="flex h-6 flex-1 items-center gap-[3px]" aria-hidden="true">
      <span
        v-for="(height, index) in bars"
        :key="index"
        class="wave-bar w-[3px] rounded-full bg-[#a340c4]/45"
        :class="playing ? 'is-playing' : ''"
        :style="{ height: `${Math.round(height * 100)}%`, '--bar-delay': `${index * 70}ms` }"
      />
    </div>

    <audio
      ref="audio"
      :src="src"
      preload="none"
      class="sr-only"
      @play="onPlay"
      @pause="onStop"
      @ended="onStop"
      @error="failed = true"
    />
  </div>
</template>

<style scoped>
.wave-bar {
  transition: background-color 0.2s ease;
}

.wave-bar.is-playing {
  background-color: #a340c4;
  animation: pulse 900ms ease-in-out infinite alternate;
  animation-delay: var(--bar-delay);
}

@keyframes pulse {
  from {
    transform: scaleY(0.45);
  }
  to {
    transform: scaleY(1);
  }
}

@media (prefers-reduced-motion: reduce) {
  .wave-bar.is-playing {
    animation: none;
  }
}
</style>
