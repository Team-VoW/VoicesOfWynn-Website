<script setup lang="ts">
import { computed } from 'vue'

type Size = 'sm' | 'md' | 'lg' | 'xl'

const props = withDefaults(
  defineProps<{
    src: string
    fallbackSrc?: string
    alt?: string
    size?: Size
    interactive?: boolean
  }>(),
  {
    fallbackSrc: '',
    alt: 'Profile picture',
    size: 'md',
    interactive: true,
  },
)

const sizeClass = computed(() => {
  switch (props.size) {
    case 'sm':
      return 'size-12'
    case 'lg':
      return 'size-36'
    case 'xl':
      return 'size-44'
    case 'md':
    default:
      return 'size-20'
  }
})

const ringWidth = computed(() => (props.size === 'sm' ? '3px' : '5px'))

function onError(event: Event) {
  if (!props.fallbackSrc) return
  const img = event.target as HTMLImageElement
  if (img.src !== props.fallbackSrc) img.src = props.fallbackSrc
}
</script>

<template>
  <span
    :class="[
      'profile-avatar relative inline-flex shrink-0 items-center justify-center rounded-full p-[var(--ring-w)]',
      sizeClass,
      interactive ? 'profile-avatar--interactive' : '',
    ]"
    :style="{
      background: 'linear-gradient(135deg, #7c00b4, #ff6b9d)',
      '--ring-w': ringWidth,
    }"
  >
    <img
      :key="src"
      :src="src"
      :alt="alt"
      class="size-full rounded-full bg-white object-cover"
      @error="onError"
    />
  </span>
</template>

<style scoped>
.profile-avatar--interactive {
  transition: transform 300ms ease-out, box-shadow 300ms ease-out;
  will-change: transform;
}

.profile-avatar--interactive:hover,
.profile-avatar--interactive:focus-within {
  transform: scale(1.03);
  box-shadow: 0 6px 20px rgba(123, 26, 155, 0.2);
}

@media (prefers-reduced-motion: reduce) {
  .profile-avatar--interactive {
    transition: none;
  }
  .profile-avatar--interactive:hover,
  .profile-avatar--interactive:focus-within {
    transform: none;
    box-shadow: none;
  }
}
</style>
