<script setup lang="ts">
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import type { AggregateUsageResponse } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useAggregateUsage } from '../queries'

const aggregate = useAggregateUsage()
const result = ref<AggregateUsageResponse | null>(null)
const error = ref('')

const isRunning = computed(() => aggregate.isPending.value)

async function run() {
  error.value = ''
  result.value = null

  try {
    const response = await aggregate.mutateAsync()
    result.value = response
    toast.success(
      response.daysProcessed === 0
        ? 'Already up to date — nothing left to aggregate.'
        : `Aggregated ${response.bootupsAggregated} bootup${response.bootupsAggregated === 1 ? '' : 's'} across ${response.daysProcessed} day${response.daysProcessed === 1 ? '' : 's'}.`,
    )
  } catch (err) {
    error.value = messageFromContentError(err)
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="space-y-1">
      <h2 class="text-sm font-semibold">Aggregate usage analytics</h2>
      <p class="text-sm text-muted-foreground">
        Rolls raw mod bootup pings into the daily totals the analytics chart reads, then discards
        the raw rows. Only days that can no longer receive pings are processed, so the two most
        recent days are always left alone.
      </p>
    </div>

    <div class="flex flex-wrap items-center gap-3">
      <Button type="button" :disabled="isRunning" @click="run">
        {{ isRunning ? 'Aggregating…' : 'Run aggregation' }}
      </Button>
      <p class="text-xs text-muted-foreground">
        Safe to run as often as you like — days already aggregated are skipped.
      </p>
    </div>

    <div
      v-if="error"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ error }}
    </div>

    <div v-if="result" class="grid grid-cols-1 gap-2 text-sm sm:grid-cols-3">
      <div class="rounded-md border p-3">
        <div class="text-muted-foreground">Days processed</div>
        <div class="text-lg font-semibold">{{ result.daysProcessed }}</div>
      </div>
      <div class="rounded-md border p-3">
        <div class="text-muted-foreground">Bootups aggregated</div>
        <div class="text-lg font-semibold">{{ result.bootupsAggregated }}</div>
      </div>
      <div class="rounded-md border p-3">
        <div class="text-muted-foreground">Through</div>
        <div class="text-lg font-semibold">{{ result.throughDate ?? '—' }}</div>
      </div>
    </div>
  </section>
</template>
