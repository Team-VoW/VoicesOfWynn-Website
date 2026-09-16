<script setup lang="ts">
import { computed, ref } from 'vue'
import { Play } from 'lucide-vue-next'

const props = defineProps<{
  videoId: string
  /** Spoken label for the play control, e.g. "Play the Voices of Wynn trailer". */
  label: string
}>()

const playing = ref(false)

// The YouTube iframe is only created once someone asks for it, so an anonymous
// visit to the homepage loads nothing from youtube.com beyond the thumbnail.
const embedUrl = computed(
  () => `https://www.youtube-nocookie.com/embed/${props.videoId}?autoplay=1`,
)
const thumbnailUrl = computed(() => `https://img.youtube.com/vi/${props.videoId}/hqdefault.jpg`)
const iframePermissions =
  'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture'
</script>

<template>
  <div
    class="relative aspect-video w-full overflow-hidden rounded-xl border-2 border-[#fbd057]/60 bg-[#1b0f2b] shadow-[0_18px_50px_rgba(20,8,36,0.55)] has-[button:focus-visible]:shadow-[0_0_0_3px_#2e1a47,0_0_0_6px_#fbd057,0_18px_50px_rgba(20,8,36,0.55)]"
  >
    <iframe
      v-if="playing"
      class="absolute inset-0 size-full"
      :src="embedUrl"
      :title="label"
      :allow="iframePermissions"
      allowfullscreen
    />
    <button
      v-else
      type="button"
      class="group absolute inset-0 size-full cursor-pointer outline-none"
      :aria-label="label"
      @click="playing = true"
    >
      <img
        :src="thumbnailUrl"
        alt=""
        loading="lazy"
        decoding="async"
        class="size-full object-cover transition-transform duration-300 group-hover:scale-[1.03] motion-reduce:transition-none motion-reduce:group-hover:scale-100"
      />
      <span
        class="absolute inset-0 bg-[linear-gradient(to_top,rgba(46,26,71,0.75),rgba(46,26,71,0.05))]"
      />
      <span
        class="absolute left-1/2 top-1/2 flex size-16 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-[#fbd057] text-[#2a1438] shadow-lg transition-transform duration-300 group-hover:scale-110 motion-reduce:transition-none motion-reduce:group-hover:scale-100"
      >
        <Play class="size-7 translate-x-0.5 fill-current" aria-hidden="true" />
      </span>
    </button>
  </div>
</template>
