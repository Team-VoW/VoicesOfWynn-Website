<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { refDebounced } from '@vueuse/core'
import ReportColumnVisibility from '../components/ReportColumnVisibility.vue'
import ReportFilters from '../components/ReportFilters.vue'
import ReportTable from '../components/ReportTable.vue'
import ReportPagination from '../components/ReportPagination.vue'
import { useReportsSearch } from '../queries'
import {
  REPORT_SORT_FIELDS,
  REPORT_STATUSES,
  type ReportSearchRequest,
  type ReportSortField,
  type ReportStatus,
  type SortDirection,
} from '@/api/types'

const PAGE_SIZE = 25
const route = useRoute()
const router = useRouter()

function statusFromQuery(value: unknown): ReportStatus | 'any' {
  return typeof value === 'string' && (REPORT_STATUSES as string[]).includes(value)
    ? (value as ReportStatus)
    : 'any'
}

function sortByFromQuery(value: unknown): ReportSortField | undefined {
  return typeof value === 'string' && (REPORT_SORT_FIELDS as string[]).includes(value)
    ? (value as ReportSortField)
    : undefined
}

function sortDirFromQuery(value: unknown): SortDirection | undefined {
  return value === 'asc' || value === 'desc' ? value : undefined
}

function stringFromQuery(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

const npc = ref(stringFromQuery(route.query.npc))
const content = ref(stringFromQuery(route.query.content))
const status = ref<ReportStatus | 'any'>(statusFromQuery(route.query.status))
const sortBy = ref<ReportSortField | undefined>(sortByFromQuery(route.query.sortBy))
const sortDir = ref<SortDirection | undefined>(sortDirFromQuery(route.query.sortDir))
const page = ref(Number(route.query.page) > 0 ? Number(route.query.page) : 1)

const npcDebounced = refDebounced(npc, 300)
const contentDebounced = refDebounced(content, 300)

watch([npcDebounced, contentDebounced, status, sortBy, sortDir], () => {
  page.value = 1
})

const params = computed<ReportSearchRequest>(() => ({
  npc: npcDebounced.value || undefined,
  content: contentDebounced.value || undefined,
  status: status.value === 'any' ? undefined : status.value,
  sortBy: sortBy.value,
  sortDir: sortBy.value ? sortDir.value : undefined,
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
        ...(p.sortBy ? { sortBy: p.sortBy } : {}),
        ...(p.sortBy && p.sortDir ? { sortDir: p.sortDir } : {}),
        ...(p.page > 1 ? { page: String(p.page) } : {}),
      },
    })
  },
  { immediate: false },
)

// Re-seed local state when only the query changes (e.g. browser back/forward).
// Vue Router reuses the component instance, so without this the inputs stay stale.
watch(
  () => route.query,
  (q) => {
    const nextNpc = stringFromQuery(q.npc)
    const nextContent = stringFromQuery(q.content)
    const nextStatus = statusFromQuery(q.status)
    const nextSortBy = sortByFromQuery(q.sortBy)
    const nextSortDir = sortDirFromQuery(q.sortDir)
    const nextPage = Number(q.page) > 0 ? Number(q.page) : 1

    if (nextNpc !== npc.value) npc.value = nextNpc
    if (nextContent !== content.value) content.value = nextContent
    if (nextStatus !== status.value) status.value = nextStatus
    if (nextSortBy !== sortBy.value) sortBy.value = nextSortBy
    if (nextSortDir !== sortDir.value) sortDir.value = nextSortDir
    if (nextPage !== page.value) page.value = nextPage
  },
)

const { data, isLoading, isFetching, isError, error } = useReportsSearch(params)
const results = computed(() => data.value?.results ?? [])
const total = computed(() => data.value?.total ?? 0)

function onSortChange(nextSortBy: ReportSortField | undefined, nextSortDir: SortDirection | undefined) {
  sortBy.value = nextSortBy
  sortDir.value = nextSortDir
}
</script>

<template>
  <div class="mx-auto max-w-screen-2xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Reported lines</h1>
      <p class="text-sm text-muted-foreground">
        Search reports by NPC, message content, or status.
      </p>
    </header>

    <div class="flex items-end gap-3">
      <ReportFilters
        v-model:npc="npc"
        v-model:content="content"
        v-model:status="status"
        class="flex-1"
      />
      <ReportColumnVisibility />
    </div>

    <div v-if="isError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
      Failed to load reports: {{ (error as Error)?.message ?? 'unknown error' }}
    </div>

    <ReportTable
      :results="results"
      :loading="isLoading || isFetching"
      :sort-by="sortBy"
      :sort-dir="sortDir"
      @update:sort="onSortChange"
    />

    <ReportPagination
      :page="page"
      :page-size="PAGE_SIZE"
      :total="total"
      @update:page="(v) => (page = v)"
    />
  </div>
</template>
