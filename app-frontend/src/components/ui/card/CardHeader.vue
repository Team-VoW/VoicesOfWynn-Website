<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from '@/lib/utils'

const props = defineProps<{
  class?: HTMLAttributes['class']
}>()
</script>

<template>
  <div data-slot="card-header" :class="cn('space-y-1.5 p-5 pb-3', props.class)">
    <slot />
  </div>
</template>
