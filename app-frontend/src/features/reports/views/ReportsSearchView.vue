<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { refDebounced } from '@vueuse/core'
import ReportColumnVisibility from '../components/ReportColumnVisibility.vue'
import ReportFilters from '../components/ReportFilters.vue'
import ReportManageDrawer from '../components/ReportManageDrawer.vue'
import ReportTable from '../components/ReportTable.vue'
import ReportPagination from '../components/ReportPagination.vue'
import { useReportsSearch } from '../queries'
import {
  REPORT_SORT_FIELDS,
  REPORT_STATUSES,
  type ReportSearchRequest,
  type ReportSearchResult,
  type ReportSortField,
  type ReportStatus,
  type SortDirection,
} from '@/api/types'

const DEFAULT_PAGE_SIZE = 25
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const
const route = useRoute()
const router = useRouter()

function statusFromQuery(value: unknown): ReportStatus | 'any' {
  return typeof value === 'string' && (REPORT_STATUSES as string[]).includes(value)
    ? (value as ReportStatus)
    : 'any'
}

function sortByFromQuery(value: unknown): ReportSortField | undefined {
  return typeof value === 'string' && REPORT_SORT_FIELDS.includes(value as ReportSortField)
    ? (value as ReportSortField)
    : undefined
}

function sortDirFromQuery(value: unknown): SortDirection | undefined {
  return value === 'asc' || value === 'desc' ? value : undefined
}

function stringFromQuery(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function pageSizeFromQuery(value: unknown): number {
  const pageSize = Number(value)
  return PAGE_SIZE_OPTIONS.includes(pageSize as typeof PAGE_SIZE_OPTIONS[number])
    ? pageSize
    : DEFAULT_PAGE_SIZE
}

const npc = ref(stringFromQuery(route.query.npc))
const content = ref(stringFromQuery(route.query.content))
const status = ref<ReportStatus | 'any'>(statusFromQuery(route.query.status))
const sortBy = ref<ReportSortField | undefined>(sortByFromQuery(route.query.sortBy))
const sortDir = ref<SortDirection | undefined>(sortDirFromQuery(route.query.sortDir))
const page = ref(Number(route.query.page) > 0 ? Number(route.query.page) : 1)
const pageSize = ref(pageSizeFromQuery(route.query.pageSize))

const npcDebounced = refDebounced(npc, 300)
const contentDebounced = refDebounced(content, 300)

watch([npcDebounced, contentDebounced, status, sortBy, sortDir], () => {
  page.value = 1
})

watch(pageSize, () => {
  page.value = 1
})

const params = computed<ReportSearchRequest>(() => ({
  npc: npcDebounced.value || undefined,
  content: contentDebounced.value || undefined,
  status: status.value === 'any' ? undefined : status.value,
  sortBy: sortBy.value,
  sortDir: sortBy.value ? sortDir.value : undefined,
  page: page.value,
  pageSize: pageSize.value,
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
        ...(p.pageSize !== DEFAULT_PAGE_SIZE ? { pageSize: String(p.pageSize) } : {}),
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
    const nextPageSize = pageSizeFromQuery(q.pageSize)

    if (nextNpc !== npc.value) npc.value = nextNpc
    if (nextContent !== content.value) content.value = nextContent
    if (nextStatus !== status.value) status.value = nextStatus
    if (nextSortBy !== sortBy.value) sortBy.value = nextSortBy
    if (nextSortDir !== sortDir.value) sortDir.value = nextSortDir
    if (nextPage !== page.value) page.value = nextPage
    if (nextPageSize !== pageSize.value) pageSize.value = nextPageSize
  },
)

const { data, isLoading, isFetching, isError, error } = useReportsSearch(params)
const results = computed(() => data.value?.results ?? [])
const total = computed(() => data.value?.total ?? 0)

function onSortChange(nextSortBy: ReportSortField | undefined, nextSortDir: SortDirection | undefined) {
  sortBy.value = nextSortBy
  sortDir.value = nextSortDir
}

const selectedReportId = ref<number | null>(null)
const drawerOpen = ref(false)
const selectedReport = computed<ReportSearchResult | null>(() =>
  selectedReportId.value === null
    ? null
    : results.value.find((r) => r.reportId === selectedReportId.value) ?? null,
)

function onManage(report: ReportSearchResult) {
  selectedReportId.value = report.reportId
  drawerOpen.value = true
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
      @manage="onManage"
    />

    <ReportPagination
      :page="page"
      :page-size="pageSize"
      :page-size-options="PAGE_SIZE_OPTIONS"
      :total="total"
      @update:page="(v) => (page = v)"
      @update:page-size="(v) => (pageSize = v)"
    />

    <ReportManageDrawer v-model:open="drawerOpen" :report="selectedReport" />
  </div>
</template>
