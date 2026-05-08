<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { refDebounced } from '@vueuse/core'
import ReportFilters from '../components/ReportFilters.vue'
import ReportTable from '../components/ReportTable.vue'
import ReportPagination from '../components/ReportPagination.vue'
import { useReportsSearch } from '../queries'
import { REPORT_STATUSES, type ReportSearchRequest, type ReportStatus } from '@/api/types'

const PAGE_SIZE = 25
const route = useRoute()
const router = useRouter()

function statusFromQuery(value: unknown): ReportStatus | 'any' {
  return typeof value === 'string' && (REPORT_STATUSES as string[]).includes(value)
    ? (value as ReportStatus)
    : 'any'
}

function stringFromQuery(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

const npc = ref(stringFromQuery(route.query.npc))
const content = ref(stringFromQuery(route.query.content))
const status = ref<ReportStatus | 'any'>(statusFromQuery(route.query.status))
const page = ref(Number(route.query.page) > 0 ? Number(route.query.page) : 1)

const npcDebounced = refDebounced(npc, 300)
const contentDebounced = refDebounced(content, 300)

watch([npcDebounced, contentDebounced, status], () => {
  page.value = 1
})

const params = computed<ReportSearchRequest>(() => ({
  npc: npcDebounced.value || undefined,
  content: contentDebounced.value || undefined,
  status: status.value === 'any' ? undefined : status.value,
  page: page.value,
  pageSize: PAGE_SIZE,
}))

watch(
  params,
  (p) => {
    void router.replace({
      query: {
        ...(p.npc ? { npc: p.npc } : {}),
        ...(p.content ? { content: p.content } : {}),
        ...(p.status ? { status: p.status } : {}),
        ...(p.page > 1 ? { page: String(p.page) } : {}),
      },
    })
  },
  { immediate: false },
)

const { data, isLoading, isFetching, isError, error } = useReportsSearch(params)
const results = computed(() => data.value?.results ?? [])
const total = computed(() => data.value?.total ?? 0)
</script>

<template>
  <div class="space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Reported lines</h1>
      <p class="text-sm text-muted-foreground">
        Search reports by NPC, message content, or status.
      </p>
    </header>

    <ReportFilters v-model:npc="npc" v-model:content="content" v-model:status="status" />

    <div v-if="isError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
      Failed to load reports: {{ (error as Error)?.message ?? 'unknown error' }}
    </div>

    <ReportTable :results="results" :loading="isLoading || isFetching" />

    <ReportPagination
      :page="page"
      :page-size="PAGE_SIZE"
      :total="total"
      @update:page="(v) => (page = v)"
    />
  </div>
</template>
