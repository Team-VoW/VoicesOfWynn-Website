<script setup lang="ts">
import type { DialogTriggerProps } from 'reka-ui'
import { DialogTrigger, useForwardProps } from 'reka-ui'

const props = defineProps<DialogTriggerProps>()
const forwarded = useForwardProps(props)
</script>

<template>
  <DialogTrigger data-slot="sheet-trigger" v-bind="forwarded">
    <slot />
  </DialogTrigger>
</template>
