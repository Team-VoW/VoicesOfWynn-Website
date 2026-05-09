<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { Loader2, Trash2 } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { REPORT_STATUSES, type ReportSearchResult, type ReportStatus } from '@/api/types'
import { useDeleteReport, useUpdateReportStatus } from '../queries'

const props = defineProps<{
  open: boolean
  report: ReportSearchResult | null
}>()

const emit = defineEmits<{ (e: 'update:open', value: boolean): void }>()

const updateStatus = useUpdateReportStatus()
const deleteReport = useDeleteReport()

const confirmingDelete = ref(false)
// Local status mirrors the report so the Select reflects optimistic changes
// without waiting for the search query to refetch.
const localStatus = ref<ReportStatus | undefined>(props.report?.status)

watch(
  () => props.report,
  (r) => {
    localStatus.value = r?.status
    confirmingDelete.value = false
  },
)

watch(
  () => props.open,
  (open) => {
    if (!open) confirmingDelete.value = false
  },
)

const busy = computed(() => updateStatus.isPending.value || deleteReport.isPending.value)

function onOpenChange(value: boolean) {
  if (busy.value && !value) return
  emit('update:open', value)
}

function onStatusChange(value: unknown) {
  if (typeof value !== 'string') return
  const status = value as ReportStatus
  if (!props.report || status === props.report.status) return
  const reportId = props.report.reportId
  const previous = localStatus.value
  localStatus.value = status
  updateStatus.mutate(
    { reportId, status },
    {
      onSuccess: () => toast.success(`Status set to ${status}`),
      onError: () => {
        localStatus.value = previous
        toast.error('Failed to update status')
      },
    },
  )
}

function onDelete() {
  if (!props.report) return
  const reportId = props.report.reportId
  deleteReport.mutate(reportId, {
    onSuccess: () => {
      toast.success('Report deleted')
      emit('update:open', false)
    },
    onError: () => toast.error('Failed to delete report'),
  })
}
</script>

<template>
  <Sheet :open="open" @update:open="onOpenChange">
    <SheetContent v-if="report">
      <SheetHeader>
        <SheetTitle>Manage report #{{ report.reportId }}</SheetTitle>
        <SheetDescription>
          NPC: <span class="font-medium text-foreground">{{ report.npcName ?? '—' }}</span>
        </SheetDescription>
        <p class="mt-3 line-clamp-4 rounded-md bg-muted/40 p-3 text-sm text-muted-foreground">
          {{ report.chatMessage }}
        </p>
      </SheetHeader>

      <div class="space-y-2 px-6">
        <Label for="manage-status">Status</Label>
        <Select :model-value="localStatus" :disabled="busy" @update:model-value="onStatusChange">
          <SelectTrigger id="manage-status" class="w-full">
            <SelectValue placeholder="Select status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="s in REPORT_STATUSES" :key="s" :value="s">{{ s }}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <SheetFooter>
        <div v-if="!confirmingDelete">
          <Button
            variant="destructive"
            class="w-full"
            :disabled="busy"
            @click="confirmingDelete = true"
          >
            <Trash2 class="size-4" />
            Delete report
          </Button>
        </div>
        <div v-else class="space-y-2 rounded-md border border-destructive/40 bg-destructive/5 p-3">
          <p class="text-sm font-medium">Delete this report? This cannot be undone.</p>
          <div class="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              class="flex-1"
              :disabled="busy"
              @click="confirmingDelete = false"
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              size="sm"
              class="flex-1"
              :disabled="busy"
              @click="onDelete"
            >
              <Loader2 v-if="deleteReport.isPending.value" class="size-4 animate-spin" />
              Yes, delete
            </Button>
          </div>
        </div>
      </SheetFooter>
    </SheetContent>
  </Sheet>
</template>
