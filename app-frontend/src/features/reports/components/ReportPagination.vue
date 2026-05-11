<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const props = defineProps<{
  page: number
  pageSize: number
  pageSizeOptions: readonly number[]
  total: number
}>()

const emit = defineEmits<{
  (e: 'update:page', value: number): void
  (e: 'update:pageSize', value: number): void
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
const pageInput = ref(String(currentPage.value))
const pageSizeValue = computed({
  get: () => String(props.pageSize),
  set: (value: string) => {
    const pageSize = Number(value)
    if (Number.isFinite(pageSize) && pageSize > 0) {
      emit('update:pageSize', pageSize)
    }
  },
})

watch(currentPage, (value) => {
  pageInput.value = String(value)
})

function commitPageInput() {
  const requestedPage = Number(pageInput.value)

  if (!Number.isFinite(requestedPage)) {
    pageInput.value = String(currentPage.value)
    return
  }

  const nextPage = Math.min(Math.max(1, Math.trunc(requestedPage)), totalPages.value)
  pageInput.value = String(nextPage)

  if (nextPage !== props.page) {
    emit('update:page', nextPage)
  }
}
</script>

<template>
  <div class="flex flex-col gap-3 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
    <p>
      <template v-if="total > 0">
        Showing <span class="text-foreground">{{ rangeStart }}–{{ rangeEnd }}</span> of
        <span class="text-foreground">{{ total }}</span>
      </template>
      <template v-else>No results</template>
    </p>
    <div class="flex flex-wrap items-center gap-3">
      <div class="flex items-center gap-2">
        <Label for="reports-page-size" class="text-muted-foreground">Rows per page</Label>
        <Select v-model="pageSizeValue">
          <SelectTrigger id="reports-page-size" size="sm" class="w-20">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="option in pageSizeOptions" :key="option" :value="String(option)">
              {{ option }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div class="flex items-center gap-2">
        <Button variant="outline" size="sm" :disabled="!canPrev" @click="emit('update:page', currentPage - 1)">
          Previous
        </Button>
        <span class="tabular-nums">Page {{ currentPage }} / {{ totalPages }}</span>
        <div class="flex items-center gap-2">
          <Label for="reports-page-jump" class="sr-only">Go to page</Label>
          <Input
            id="reports-page-jump"
            v-model="pageInput"
            type="number"
            inputmode="numeric"
            min="1"
            :max="totalPages"
            class="h-8 w-20 tabular-nums"
            aria-label="Go to page"
            @blur="commitPageInput"
            @keydown.enter.prevent="commitPageInput"
          />
          <Button variant="outline" size="sm" @click="commitPageInput">Go</Button>
        </div>
        <Button variant="outline" size="sm" :disabled="!canNext" @click="emit('update:page', currentPage + 1)">
          Next
        </Button>
      </div>
    </div>
  </div>
</template>
