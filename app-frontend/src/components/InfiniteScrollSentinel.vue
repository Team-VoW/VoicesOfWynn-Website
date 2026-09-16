<script setup lang="ts">
import { ref, watchEffect } from 'vue'
import { useIntersectionObserver } from '@vueuse/core'

const props = defineProps<{ disabled?: boolean }>()
const emit = defineEmits<{ (e: 'load'): void }>()

const sentinel = ref<HTMLElement | null>(null)
const isIntersecting = ref(false)

// rootMargin starts the next page while the sentinel is still below the fold, so the list grows
// before the reader reaches the end of what is already rendered.
useIntersectionObserver(
  sentinel,
  ([entry]) => {
    isIntersecting.value = entry?.isIntersecting ?? false
  },
  { rootMargin: '400px' },
)

// Driven by state rather than by the intersection event itself. After a page loads, the sentinel
// is usually still in view, so its intersection never changes and an event-driven version would
// stall: this fires again as soon as the load finishes while it is still visible.
watchEffect(() => {
  if (isIntersecting.value && !props.disabled) emit('load')
})
</script>

<template>
  <div ref="sentinel" aria-hidden="true" class="h-px w-full" />
</template>
