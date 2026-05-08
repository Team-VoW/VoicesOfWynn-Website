<script setup lang="ts">
import { computed } from 'vue'
import { Button } from '@/components/ui/button'

const props = defineProps<{
  page: number
  pageSize: number
  total: number
}>()

const emit = defineEmits<{
  (e: 'update:page', value: number): void
}>()

const totalPages = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))
const canPrev = computed(() => props.page > 1)
const canNext = computed(() => props.page < totalPages.value)
const rangeStart = computed(() => (props.total === 0 ? 0 : (props.page - 1) * props.pageSize + 1))
const rangeEnd = computed(() => Math.min(props.total, props.page * props.pageSize))
</script>

<template>
  <div class="flex items-center justify-between gap-4 text-sm text-muted-foreground">
    <p>
      <template v-if="total > 0">
        Showing <span class="text-foreground">{{ rangeStart }}–{{ rangeEnd }}</span> of
        <span class="text-foreground">{{ total }}</span>
      </template>
      <template v-else>No results</template>
    </p>
    <div class="flex items-center gap-2">
      <Button variant="outline" size="sm" :disabled="!canPrev" @click="emit('update:page', page - 1)">
        Previous
      </Button>
      <span class="tabular-nums">Page {{ page }} / {{ totalPages }}</span>
      <Button variant="outline" size="sm" :disabled="!canNext" @click="emit('update:page', page + 1)">
        Next
      </Button>
    </div>
  </div>
</template>
