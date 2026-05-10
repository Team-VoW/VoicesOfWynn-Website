<script setup lang="ts">
import type { ToggleGroupRootEmits, ToggleGroupRootProps } from 'reka-ui'
import type { HTMLAttributes } from 'vue'
import { ToggleGroupRoot, useForwardPropsEmits } from 'reka-ui'
import { computed } from 'vue'
import { cn } from '@/lib/utils'

const props = defineProps<ToggleGroupRootProps & {
  class?: HTMLAttributes['class']
}>()
const emits = defineEmits<ToggleGroupRootEmits>()

const delegatedProps = computed(() => {
  const { class: _, ...rest } = props
  return rest
})

const forwarded = useForwardPropsEmits(delegatedProps, emits)
</script>

<template>
  <ToggleGroupRoot
    data-slot="toggle-group"
    v-bind="forwarded"
    :class="cn('inline-flex items-center rounded-md border bg-background p-1', props.class)"
  >
    <slot />
  </ToggleGroupRoot>
</template>
