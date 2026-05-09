<script setup lang="ts">
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
import type { ReportSearchResult, ReportStatus } from '@/api/types'

defineProps<{
  results: ReportSearchResult[]
  loading: boolean
}>()

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
</script>

<template>
  <div class="relative rounded-md border" :aria-busy="loading">
    <div v-if="loading && results.length > 0" class="absolute inset-x-0 top-0 z-10 h-0.5 animate-pulse bg-primary" />
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead class="w-[160px]">NPC</TableHead>
          <TableHead>Message</TableHead>
          <TableHead class="w-[120px]">Status</TableHead>
          <TableHead class="w-[80px] text-right">Reports</TableHead>
          <TableHead class="w-[180px]">Submitted</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-if="loading && results.length === 0">
          <TableRow v-for="i in 5" :key="i">
            <TableCell><Skeleton class="h-4 w-24" /></TableCell>
            <TableCell><Skeleton class="h-4 w-full" /></TableCell>
            <TableCell><Skeleton class="h-5 w-16" /></TableCell>
            <TableCell class="text-right"><Skeleton class="ml-auto h-4 w-8" /></TableCell>
            <TableCell><Skeleton class="h-4 w-32" /></TableCell>
          </TableRow>
        </template>
        <TableEmpty v-else-if="results.length === 0" :colspan="5">No reports match the filters.</TableEmpty>
        <TableRow v-for="r in results" v-else :key="r.reportId" :class="loading && 'opacity-60'">
          <TableCell class="font-medium">{{ r.npcName ?? '—' }}</TableCell>
          <TableCell>
            <div class="max-w-md truncate">{{ r.chatMessage }}</div>
          </TableCell>
          <TableCell>
            <Badge :variant="statusVariant[r.status]">{{ r.status }}</Badge>
          </TableCell>
          <TableCell class="text-right tabular-nums">{{ r.reportedTimes }}</TableCell>
          <TableCell class="text-muted-foreground">{{ formatDate(r.timeSubmitted) }}</TableCell>
        </TableRow>
      </TableBody>
    </Table>
  </div>
</template>
