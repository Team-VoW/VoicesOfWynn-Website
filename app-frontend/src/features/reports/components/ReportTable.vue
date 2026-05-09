<script setup lang="ts">
import { computed } from 'vue'
import { ChevronDown, ChevronUp, ChevronsUpDown } from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableEmpty,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type {
  ReportSearchResult,
  ReportSortField,
  ReportStatus,
  SortDirection,
} from '@/api/types'
import TruncatedCell from './TruncatedCell.vue'
import { useReportColumnVisibility } from '../composables/useReportColumnVisibility'

const props = defineProps<{
  results: ReportSearchResult[]
  loading: boolean
  sortBy?: ReportSortField
  sortDir?: SortDirection
}>()

const emit = defineEmits<{
  (e: 'update:sort', sortBy: ReportSortField | undefined, sortDir: SortDirection | undefined): void
}>()

interface Column {
  key: ReportSortField
  label: string
  headClass?: string
  align?: 'left' | 'right'
}

const columns: Column[] = [
  { key: 'npcName', label: 'NPC', headClass: 'w-[180px]' },
  { key: 'chatMessage', label: 'Message' },
  { key: 'status', label: 'Status', headClass: 'w-[120px]' },
  { key: 'reportedTimes', label: 'Reports', headClass: 'w-[100px]', align: 'right' },
  { key: 'timeSubmitted', label: 'Submitted', headClass: 'w-[200px]' },
]

const { visibility } = useReportColumnVisibility()
const visibleColumns = computed(() => columns.filter((c) => visibility.value[c.key]))

const statusVariant: Record<ReportStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  unprocessed: 'secondary',
  forwarded: 'outline',
  rejected: 'destructive',
  accepted: 'default',
  fixed: 'default',
}

const dateFmt = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
function formatDate(iso: string) {
  return dateFmt.format(new Date(iso))
}

function cycleSort(key: ReportSortField) {
  if (props.sortBy !== key) {
    emit('update:sort', key, 'asc')
    return
  }
  if (props.sortDir === 'asc') {
    emit('update:sort', key, 'desc')
    return
  }
  emit('update:sort', undefined, undefined)
}

const skeletonClassFor: Record<ReportSortField, string> = {
  npcName: 'h-4 w-24',
  chatMessage: 'h-4 w-full',
  status: 'h-5 w-16',
  reportedTimes: 'ml-auto h-4 w-8',
  timeSubmitted: 'h-4 w-32',
}
</script>

<template>
  <div class="relative rounded-md border" :aria-busy="loading">
    <div v-if="loading && results.length > 0" class="absolute inset-x-0 top-0 z-10 h-0.5 animate-pulse bg-primary" />
    <Table class="table-fixed min-w-[900px]">
      <TableHeader>
        <TableRow>
          <TableHead
            v-for="col in visibleColumns"
            :key="col.key"
            :class="col.headClass"
          >
            <button
              type="button"
              class="-mx-2 flex w-full items-center gap-1.5 rounded-sm px-2 py-1 text-left font-medium transition-colors hover:bg-muted/60 focus-visible:outline-2 focus-visible:outline-ring"
              :class="col.align === 'right' && 'justify-end'"
              :aria-sort="sortBy === col.key ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none'"
              @click="cycleSort(col.key)"
            >
              <span>{{ col.label }}</span>
              <ChevronUp v-if="sortBy === col.key && sortDir === 'asc'" class="size-3.5" />
              <ChevronDown v-else-if="sortBy === col.key && sortDir === 'desc'" class="size-3.5" />
              <ChevronsUpDown v-else class="size-3.5 text-muted-foreground/60" />
            </button>
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-if="loading && results.length === 0">
          <TableRow v-for="i in 5" :key="i">
            <TableCell
              v-for="col in visibleColumns"
              :key="col.key"
              :class="col.align === 'right' && 'text-right'"
            >
              <Skeleton :class="skeletonClassFor[col.key]" />
            </TableCell>
          </TableRow>
        </template>
        <TableEmpty v-else-if="results.length === 0" :colspan="visibleColumns.length || 1">No reports match the filters.</TableEmpty>
        <TableRow v-for="r in results" v-else :key="r.reportId" :class="loading && 'opacity-60'">
          <template v-for="col in visibleColumns" :key="col.key">
            <TableCell v-if="col.key === 'npcName'" class="max-w-[180px] font-medium">
              <TruncatedCell :text="r.npcName ?? '—'" />
            </TableCell>
            <TableCell v-else-if="col.key === 'chatMessage'">
              <TruncatedCell :text="r.chatMessage" />
            </TableCell>
            <TableCell v-else-if="col.key === 'status'">
              <Badge :variant="statusVariant[r.status]">{{ r.status }}</Badge>
            </TableCell>
            <TableCell v-else-if="col.key === 'reportedTimes'" class="text-right tabular-nums">
              {{ r.reportedTimes }}
            </TableCell>
            <TableCell v-else-if="col.key === 'timeSubmitted'" class="text-muted-foreground">
              {{ formatDate(r.timeSubmitted) }}
            </TableCell>
          </template>
        </TableRow>
      </TableBody>
    </Table>
  </div>
</template>
