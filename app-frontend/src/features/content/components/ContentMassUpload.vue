<script setup lang="ts">
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Upload, X } from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
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
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import type { ContentOption, UploadNpcRecordingResult } from '@/api/types'
import { CONTENT_NONE, messageFromContentError, optionalContentId } from '../contentUtils'
import { useUploadMassNpcRecordings } from '../queries'

const props = defineProps<{
  isLoading: boolean
  quests: ContentOption[]
  npcs: ContentOption[]
}>()

const BATCH_FILE_LIMIT = 40
const BATCH_SIZE_LIMIT = 100 * 1024 * 1024

const selectedFiles = ref<File[]>([])
const questSelection = ref(CONTENT_NONE)
const npcSelection = ref(CONTENT_NONE)
const overwrite = ref(false)
const isDragging = ref(false)
const isUploading = ref(false)
const completedBatches = ref(0)
const totalBatches = ref(0)
const uploadError = ref('')
const results = ref<UploadNpcRecordingResult[]>([])
const uploadMutation = useUploadMassNpcRecordings()

const invalidFiles = computed(() =>
  selectedFiles.value.filter((file) => !file.name.toLowerCase().endsWith('.ogg')),
)
const totalSize = computed(() => selectedFiles.value.reduce((sum, file) => sum + file.size, 0))
const successCount = computed(() => results.value.filter((result) => result.code < 400).length)
const errorCount = computed(() => results.value.filter((result) => result.code >= 400).length)
const estimatedBatchCount = computed(() =>
  selectedFiles.value.some((file) => file.size > BATCH_SIZE_LIMIT)
    ? 0
    : createBatches(selectedFiles.value).length,
)

function addFiles(files: FileList | File[]) {
  selectedFiles.value = [...selectedFiles.value, ...Array.from(files)]
}

function onFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files) addFiles(input.files)
  input.value = ''
}

function onDrop(event: DragEvent) {
  isDragging.value = false
  if (event.dataTransfer?.files) addFiles(event.dataTransfer.files)
}

function removeFile(index: number) {
  selectedFiles.value = selectedFiles.value.filter((_, fileIndex) => fileIndex !== index)
}

function clearFiles() {
  selectedFiles.value = []
  results.value = []
  uploadError.value = ''
  completedBatches.value = 0
  totalBatches.value = 0
}

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB']
  let size = bytes / 1024
  let unitIndex = 0
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex += 1
  }
  return `${size.toFixed(size >= 10 ? 0 : 1)} ${units[unitIndex]}`
}

function recordingResultDescription(result: UploadNpcRecordingResult) {
  if (!result.conflict) return result.description

  const conflict = result.conflict
  return `${result.description} Existing location: ${conflict.questName} (quest ${conflict.questId}) -> ${conflict.npcName} (NPC ${conflict.npcId}), line ${conflict.line}, recording ${conflict.recordingId} (${conflict.fileName}).`
}

function createBatches(files: File[]) {
  const batches: File[][] = []
  let current: File[] = []
  let currentSize = 0

  for (const file of files) {
    if (file.size > BATCH_SIZE_LIMIT) {
      throw new Error(`${file.name} exceeds the ${formatBytes(BATCH_SIZE_LIMIT)} batch size limit.`)
    }

    const wouldExceedCount = current.length >= BATCH_FILE_LIMIT
    const wouldExceedSize = current.length > 0 && currentSize + file.size > BATCH_SIZE_LIMIT
    if (wouldExceedCount || wouldExceedSize) {
      batches.push(current)
      current = []
      currentSize = 0
    }

    current.push(file)
    currentSize += file.size
  }

  if (current.length > 0) batches.push(current)
  return batches
}

async function uploadSelectedFiles() {
  uploadError.value = ''
  results.value = []
  completedBatches.value = 0

  const files = selectedFiles.value.filter((file) => file.name.toLowerCase().endsWith('.ogg'))
  if (files.length === 0) {
    uploadError.value = 'Select at least one .ogg file.'
    return
  }

  try {
    const batches = createBatches(files)
    totalBatches.value = batches.length
    isUploading.value = true

    for (const batch of batches) {
      const response = await uploadMutation.mutateAsync({
        recordings: batch,
        overwrite: overwrite.value,
        questId: optionalContentId(questSelection.value),
        npcId: optionalContentId(npcSelection.value),
      })
      results.value = [...results.value, ...response.results]
      completedBatches.value += 1
    }

    toast.success(`Uploaded ${successCount.value} recording${successCount.value === 1 ? '' : 's'}.`)
  } catch (err) {
    uploadError.value = messageFromContentError(err)
  } finally {
    isUploading.value = false
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_18rem_18rem]">
      <div
        class="flex min-h-40 cursor-pointer flex-col items-center justify-center gap-3 rounded-md border border-dashed p-6 text-center transition-colors"
        :class="{ 'border-primary bg-primary/5': isDragging }"
        @dragover.prevent="isDragging = true"
        @dragleave.prevent="isDragging = false"
        @drop.prevent="onDrop"
      >
        <Upload class="size-8 text-muted-foreground" />
        <div class="space-y-1">
          <p class="text-sm font-medium">Drop OGG recordings here</p>
          <p class="text-sm text-muted-foreground">Files must be named questname-npcname-line.ogg.</p>
        </div>
        <label>
          <input class="sr-only" type="file" accept=".ogg,audio/ogg" multiple @change="onFileInput" />
          <span
            class="inline-flex h-9 items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow-xs"
          >
            Choose files
          </span>
        </label>
      </div>

      <div class="space-y-4">
        <div class="space-y-2">
          <Label for="mass-upload-quest">Quest override</Label>
          <Select v-model="questSelection" :disabled="isLoading || isUploading">
            <SelectTrigger id="mass-upload-quest" class="w-full">
              <SelectValue placeholder="Filename lookup" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem :value="CONTENT_NONE">Filename lookup</SelectItem>
              <SelectItem v-for="quest in quests" :key="quest.id" :value="String(quest.id)">
                {{ quest.name }}
              </SelectItem>
            </SelectContent>
          </Select>
          <p class="text-xs text-muted-foreground">
            Leave this on filename lookup to use the quest part of each filename. Choose a quest to
            assign every uploaded file to that quest instead.
          </p>
        </div>

        <div class="space-y-2">
          <Label for="mass-upload-npc">NPC override</Label>
          <Select v-model="npcSelection" :disabled="isLoading || isUploading">
            <SelectTrigger id="mass-upload-npc" class="w-full">
              <SelectValue placeholder="Filename lookup" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem :value="CONTENT_NONE">Filename lookup</SelectItem>
              <SelectItem v-for="npc in npcs" :key="npc.id" :value="String(npc.id)">
                {{ npc.name }}
              </SelectItem>
            </SelectContent>
          </Select>
          <p class="text-xs text-muted-foreground">
            Leave this on filename lookup to use the NPC part of each filename. Choose an NPC to
            assign every uploaded file to that NPC instead.
          </p>
        </div>
      </div>

      <div class="space-y-4">
        <div class="space-y-1">
          <label class="flex items-center gap-3 text-sm font-medium">
            <input v-model="overwrite" type="checkbox" class="size-4 rounded border-input" />
            <span>Replace files with the same name</span>
          </label>
          <p class="pl-7 text-xs text-muted-foreground">
            When off, a duplicate filename is saved as a new file like name_(1).ogg. When on, the
            existing file is replaced and no duplicate database row is added.
          </p>
        </div>

        <div class="grid grid-cols-2 gap-2 text-sm">
          <div class="rounded-md border p-3">
            <div class="text-muted-foreground">Files</div>
            <div class="text-lg font-semibold">{{ selectedFiles.length }}</div>
          </div>
          <div class="rounded-md border p-3">
            <div class="text-muted-foreground">Size</div>
            <div class="text-lg font-semibold">{{ formatBytes(totalSize) }}</div>
          </div>
          <div class="rounded-md border p-3">
            <div class="text-muted-foreground">Invalid</div>
            <div class="text-lg font-semibold">{{ invalidFiles.length }}</div>
          </div>
          <div class="rounded-md border p-3">
            <div class="text-muted-foreground">Batches</div>
            <div class="text-lg font-semibold">{{ totalBatches || estimatedBatchCount }}</div>
          </div>
        </div>

        <div class="flex flex-wrap gap-2">
          <Button
            type="button"
            :disabled="selectedFiles.length === 0 || isUploading"
            @click="uploadSelectedFiles"
          >
            Upload
          </Button>
          <Button
            type="button"
            variant="outline"
            :disabled="selectedFiles.length === 0 || isUploading"
            @click="clearFiles"
          >
            Clear
          </Button>
        </div>
      </div>
    </div>

    <div
      v-if="uploadError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ uploadError }}
    </div>

    <div v-if="isUploading || results.length > 0" class="space-y-2">
      <div class="flex flex-wrap items-center gap-2 text-sm">
        <Badge variant="secondary">Batch {{ completedBatches }} / {{ totalBatches }}</Badge>
        <Badge variant="secondary">{{ successCount }} succeeded</Badge>
        <Badge variant="secondary">{{ errorCount }} failed</Badge>
      </div>
      <div class="h-2 overflow-hidden rounded-full bg-muted">
        <div
          class="h-full bg-primary transition-all"
          :style="{ width: totalBatches ? `${(completedBatches / totalBatches) * 100}%` : '0%' }"
        />
      </div>
    </div>

    <div v-if="selectedFiles.length > 0" class="max-h-80 overflow-auto rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Selected file</TableHead>
            <TableHead class="w-28">Size</TableHead>
            <TableHead class="w-24">Type</TableHead>
            <TableHead class="w-12"></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="(file, index) in selectedFiles" :key="`${file.name}-${index}`">
            <TableCell class="font-mono text-xs">{{ file.name }}</TableCell>
            <TableCell>{{ formatBytes(file.size) }}</TableCell>
            <TableCell>
              <Badge :variant="file.name.toLowerCase().endsWith('.ogg') ? 'secondary' : 'destructive'">
                {{ file.name.toLowerCase().endsWith('.ogg') ? 'OGG' : 'Invalid' }}
              </Badge>
            </TableCell>
            <TableCell>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                :disabled="isUploading"
                @click="removeFile(index)"
              >
                <X class="size-4" />
              </Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="results.length > 0" class="overflow-auto rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead class="w-28">Status</TableHead>
            <TableHead>Original file</TableHead>
            <TableHead>Stored file</TableHead>
            <TableHead>Description</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="(result, index) in results" :key="`${result.fileName}-${index}`">
            <TableCell>
              <Badge :variant="result.code < 400 ? 'secondary' : 'destructive'">
                {{ result.code }} {{ result.message }}
              </Badge>
            </TableCell>
            <TableCell class="font-mono text-xs">{{ result.fileName }}</TableCell>
            <TableCell class="font-mono text-xs">{{ result.storedFileName ?? '-' }}</TableCell>
            <TableCell class="min-w-80 text-sm">{{ recordingResultDescription(result) }}</TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  </section>
</template>
