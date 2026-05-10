<script setup lang="ts">
import {
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Title,
  Tooltip,
  type ChartData,
  type ChartOptions,
} from 'chart.js'
import { Activity, ArrowDownRight, ArrowUpRight, CalendarDays, Flame, TrendingUp } from 'lucide-vue-next'
import { Bar, Line } from 'vue-chartjs'
import { computed, ref } from 'vue'
import { useDailyUsage } from '../queries'
import type { DailyUsagePoint, DailyUsageRange } from '@/api/types'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'

ChartJS.register(
  BarElement,
  CategoryScale,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Title,
  Tooltip,
)

const range = ref<DailyUsageRange>('90')
const { data, isLoading, isFetching, isError, error } = useDailyUsage(range)

const rangeOptions: { value: DailyUsageRange; label: string }[] = [
  { value: '30', label: '30 days' },
  { value: '90', label: '90 days' },
  { value: '365', label: '1 year' },
  { value: 'all', label: 'All' },
]

const points = computed(() => data.value?.points ?? [])
const hasData = computed(() => points.value.length > 0)

const numberFormatter = new Intl.NumberFormat()
const averageFormatter = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 1,
})
const percentFormatter = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 1,
  signDisplay: 'always',
})
const dateFormatter = new Intl.DateTimeFormat(undefined, {
  month: 'short',
  day: 'numeric',
})
const fullDateFormatter = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: 'numeric',
})

function parseDate(value: string) {
  return new Date(`${value}T00:00:00`)
}

function formatShortDate(value: string) {
  return dateFormatter.format(parseDate(value))
}

function formatFullDate(value: string) {
  return fullDateFormatter.format(parseDate(value))
}

const lineChartData = computed<ChartData<'line'>>(() => ({
  labels: points.value.map((point) => formatShortDate(point.date)),
  datasets: [
    {
      label: 'Bootups',
      data: points.value.map((point) => point.bootups),
      borderColor: '#0f766e',
      backgroundColor: 'rgba(15, 118, 110, 0.12)',
      pointBackgroundColor: '#0f766e',
      pointBorderWidth: 0,
      pointRadius: points.value.length > 120 ? 0 : 2,
      pointHoverRadius: 5,
      tension: 0.32,
      fill: true,
    },
    {
      label: '7-day average',
      data: points.value.map((point) => point.rollingAverage7Day),
      borderColor: '#f59e0b',
      backgroundColor: 'transparent',
      pointRadius: 0,
      pointHoverRadius: 4,
      borderWidth: 2,
      tension: 0.32,
    },
  ],
}))

const weekdayLabels = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const weekdayData = computed(() => {
  const totals = Array.from({ length: 7 }, () => 0)
  for (const point of points.value) {
    const weekday = parseDate(point.date).getDay()
    totals[weekday] = (totals[weekday] ?? 0) + point.bootups
  }
  return totals
})

const barChartData = computed<ChartData<'bar'>>(() => ({
  labels: weekdayLabels,
  datasets: [
    {
      label: 'Bootups',
      data: weekdayData.value,
      borderColor: '#1d4ed8',
      backgroundColor: '#3b82f6',
      borderRadius: 4,
    },
  ],
}))

const chartOptions: ChartOptions<'line'> = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: {
    mode: 'index',
    intersect: false,
  },
  plugins: {
    legend: {
      labels: {
        boxWidth: 10,
        boxHeight: 10,
      },
    },
    tooltip: {
      callbacks: {
        title(items) {
          const index = items[0]?.dataIndex
          const point = index === undefined ? undefined : points.value[index]
          return point ? formatFullDate(point.date) : ''
        },
      },
    },
  },
  scales: {
    x: {
      grid: {
        display: false,
      },
      ticks: {
        maxTicksLimit: 10,
      },
    },
    y: {
      beginAtZero: true,
      ticks: {
        precision: 0,
      },
    },
  },
}

const barChartOptions: ChartOptions<'bar'> = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: false,
    },
  },
  scales: {
    x: {
      grid: {
        display: false,
      },
    },
    y: {
      beginAtZero: true,
      ticks: {
        precision: 0,
      },
    },
  },
}

const peakDays = computed(() =>
  [...points.value]
    .sort((a, b) => b.bootups - a.bootups || a.date.localeCompare(b.date))
    .slice(0, 5),
)

const latestPoint = computed(() => points.value.at(-1) ?? null)
const changePercent = computed(() => data.value?.previousPeriodChangePercent ?? null)
const isPositiveChange = computed(() => (changePercent.value ?? 0) >= 0)

function updateRange(value: string | string[]) {
  if (typeof value === 'string' && value) {
    range.value = value as DailyUsageRange
  }
}

function formatBootups(value?: number | null) {
  return numberFormatter.format(value ?? 0)
}

function formatAverage(value?: number | null) {
  return averageFormatter.format(value ?? 0)
}

function peakDescription(point: DailyUsagePoint | null | undefined) {
  return point ? formatFullDate(point.date) : 'No peak day yet'
}
</script>

<template>
  <div class="mx-auto w-full min-w-0 max-w-screen-2xl space-y-6">
    <header class="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
      <div class="space-y-1">
        <h1 class="text-xl font-semibold tracking-normal">Usage analytics</h1>
        <p class="text-sm text-muted-foreground">
          Daily mod bootups from the API usage table.
        </p>
      </div>

      <ToggleGroup
        type="single"
        :model-value="range"
        aria-label="Analytics range"
        @update:model-value="updateRange"
      >
        <ToggleGroupItem
          v-for="option in rangeOptions"
          :key="option.value"
          :value="option.value"
        >
          {{ option.label }}
        </ToggleGroupItem>
      </ToggleGroup>
    </header>

    <div v-if="isError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
      Failed to load analytics: {{ (error as Error)?.message ?? 'unknown error' }}
    </div>

    <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0">
          <CardTitle>Total bootups</CardTitle>
          <Activity class="size-4 text-teal-700" aria-hidden="true" />
        </CardHeader>
        <CardContent>
          <Skeleton v-if="isLoading" class="h-8 w-28" />
          <p v-else class="text-2xl font-semibold tracking-normal">{{ formatBootups(data?.totalBootups) }}</p>
          <p class="mt-1 text-sm text-muted-foreground">Selected range</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0">
          <CardTitle>Daily average</CardTitle>
          <TrendingUp class="size-4 text-blue-700" aria-hidden="true" />
        </CardHeader>
        <CardContent>
          <Skeleton v-if="isLoading" class="h-8 w-24" />
          <p v-else class="text-2xl font-semibold tracking-normal">{{ formatAverage(data?.averageBootupsPerDay) }}</p>
          <p class="mt-1 text-sm text-muted-foreground">Bootups per recorded day</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0">
          <CardTitle>Peak day</CardTitle>
          <Flame class="size-4 text-amber-600" aria-hidden="true" />
        </CardHeader>
        <CardContent>
          <Skeleton v-if="isLoading" class="h-8 w-24" />
          <p v-else class="text-2xl font-semibold tracking-normal">{{ formatBootups(data?.peakDay?.bootups) }}</p>
          <p class="mt-1 text-sm text-muted-foreground">{{ peakDescription(data?.peakDay) }}</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0">
          <CardTitle>Period change</CardTitle>
          <component
            :is="isPositiveChange ? ArrowUpRight : ArrowDownRight"
            class="size-4"
            :class="isPositiveChange ? 'text-emerald-700' : 'text-destructive'"
            aria-hidden="true"
          />
        </CardHeader>
        <CardContent>
          <Skeleton v-if="isLoading" class="h-8 w-24" />
          <p v-else class="text-2xl font-semibold tracking-normal">
            {{ changePercent === null ? 'n/a' : `${percentFormatter.format(changePercent)}%` }}
          </p>
          <p class="mt-1 text-sm text-muted-foreground">Compared with previous range</p>
        </CardContent>
      </Card>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(320px,1fr)]">
      <Card class="min-w-0">
        <CardHeader class="flex flex-row items-center justify-between gap-3">
          <div>
            <CardTitle>Daily bootups</CardTitle>
            <CardDescription>Hover points to compare daily values and the rolling average.</CardDescription>
          </div>
          <Badge v-if="isFetching && !isLoading" variant="secondary">Refreshing</Badge>
        </CardHeader>
        <CardContent>
          <div v-if="isLoading" class="h-[360px] space-y-3">
            <Skeleton class="h-full w-full" />
          </div>
          <div v-else-if="!hasData" class="flex h-[360px] items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
            No daily usage data found for this range.
          </div>
          <div v-else class="relative h-[360px] w-full min-w-0">
            <Line :data="lineChartData" :options="chartOptions" />
          </div>
        </CardContent>
      </Card>

      <div class="grid min-w-0 gap-4">
        <Card class="min-w-0">
          <CardHeader>
            <CardTitle>Weekday distribution</CardTitle>
            <CardDescription>Total bootups grouped by day of week.</CardDescription>
          </CardHeader>
          <CardContent>
            <div v-if="isLoading" class="h-[220px]">
              <Skeleton class="h-full w-full" />
            </div>
            <div v-else-if="!hasData" class="flex h-[220px] items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
              No weekday data yet.
            </div>
            <div v-else class="relative h-[220px] w-full min-w-0">
              <Bar :data="barChartData" :options="barChartOptions" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader class="flex flex-row items-center justify-between space-y-0">
            <div>
              <CardTitle>Top days</CardTitle>
              <CardDescription>Highest bootup days in the selected range.</CardDescription>
            </div>
            <CalendarDays class="size-4 text-muted-foreground" aria-hidden="true" />
          </CardHeader>
          <CardContent>
            <div v-if="isLoading" class="space-y-3">
              <Skeleton v-for="index in 5" :key="index" class="h-9 w-full" />
            </div>
            <div v-else-if="!hasData" class="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
              No peak days to show.
            </div>
            <ol v-else class="space-y-2">
              <li
                v-for="(point, index) in peakDays"
                :key="point.date"
                class="flex items-center justify-between gap-3 rounded-md border px-3 py-2"
              >
                <div class="min-w-0">
                  <p class="truncate text-sm font-medium">{{ index + 1 }}. {{ formatFullDate(point.date) }}</p>
                  <p class="text-xs text-muted-foreground">7-day avg {{ formatAverage(point.rollingAverage7Day) }}</p>
                </div>
                <Badge variant="secondary">{{ formatBootups(point.bootups) }}</Badge>
              </li>
            </ol>
          </CardContent>
        </Card>
      </div>
    </section>

    <p v-if="latestPoint" class="text-xs text-muted-foreground">
      Latest recorded day: {{ formatFullDate(latestPoint.date) }}.
    </p>
  </div>
</template>
