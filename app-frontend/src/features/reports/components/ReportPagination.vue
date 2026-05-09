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

const safeTotal = computed(() => (Number.isFinite(props.total) && props.total > 0 ? props.total : 0))
const safePageSize = computed(() => (Number.isFinite(props.pageSize) && props.pageSize > 0 ? props.pageSize : 1))
const totalPages = computed(() => Math.max(1, Math.ceil(safeTotal.value / safePageSize.value)))
const currentPage = computed(() => (
  Number.isFinite(props.page)
    ? Math.min(Math.max(1, props.page), totalPages.value)
    : 1
))
const canPrev = computed(() => currentPage.value > 1)
const canNext = computed(() => currentPage.value < totalPages.value)
const rangeStart = computed(() => (safeTotal.value === 0 ? 0 : (currentPage.value - 1) * safePageSize.value + 1))
const rangeEnd = computed(() => Math.min(safeTotal.value, currentPage.value * safePageSize.value))
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
      <Button variant="outline" size="sm" :disabled="!canPrev" @click="emit('update:page', currentPage - 1)">
        Previous
      </Button>
      <span class="tabular-nums">Page {{ currentPage }} / {{ totalPages }}</span>
      <Button variant="outline" size="sm" :disabled="!canNext" @click="emit('update:page', currentPage + 1)">
        Next
      </Button>
    </div>
  </div>
</template>
