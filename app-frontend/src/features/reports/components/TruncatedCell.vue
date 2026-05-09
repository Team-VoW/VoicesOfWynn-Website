<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, useTemplateRef, watch } from 'vue'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'

const props = defineProps<{
  text: string
  class?: string
}>()

const el = useTemplateRef<HTMLDivElement>('el')
const overflowing = ref(false)
let observer: ResizeObserver | null = null

function measure() {
  const node = el.value
  if (!node) return
  overflowing.value = node.scrollWidth > node.clientWidth + 1
}

onMounted(() => {
  measure()
  if (typeof ResizeObserver !== 'undefined' && el.value) {
    observer = new ResizeObserver(measure)
    observer.observe(el.value)
  }
})

onBeforeUnmount(() => {
  observer?.disconnect()
  observer = null
})

watch(() => props.text, () => {
  // Re-measure on next paint after text content changes.
  queueMicrotask(measure)
})
</script>

<template>
  <Tooltip :disabled="!overflowing" :delay-duration="200">
    <TooltipTrigger as-child>
      <div ref="el" :class="['truncate', props.class]">{{ text }}</div>
    </TooltipTrigger>
    <TooltipContent>{{ text }}</TooltipContent>
  </Tooltip>
</template>
