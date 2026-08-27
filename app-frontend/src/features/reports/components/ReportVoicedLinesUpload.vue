<script setup lang="ts">
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Upload, X } from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { ImportVoicedLinesResponse } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useImportVoicedLines } from '../queries'

const selectedFile = ref<File | null>(null)
const isDragging = ref(false)
const uploadError = ref('')
const result = ref<ImportVoicedLinesResponse | null>(null)
const importMutation = useImportVoicedLines()

const isUploading = computed(() => importMutation.isPending.value)
const isJson = computed(() => selectedFile.value?.name.toLowerCase().endsWith('.json') ?? false)

const skipRows = computed(() => {
  const skipped = result.value?.skipped
  if (!skipped) return []

  return [
    { label: 'No audio file', value: skipped.noAudioFile },
    { label: 'Blank line', value: skipped.blankLine },
    { label: 'Line too long', value: skipped.tooLong },
    { label: 'Duplicate in file', value: skipped.duplicateInFile },
  ].filter((row) => row.value > 0)
})

function selectFile(files: FileList | File[]) {
  const [file] = Array.from(files)
  if (!file) return

  selectedFile.value = file
  uploadError.value = ''
  result.value = null
}

function onFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files) selectFile(input.files)
  input.value = ''
}

function onDrop(event: DragEvent) {
  isDragging.value = false
  if (event.dataTransfer?.files) selectFile(event.dataTransfer.files)
}

function clearFile() {
  selectedFile.value = null
  uploadError.value = ''
  result.value = null
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

async function upload() {
  const file = selectedFile.value
  if (!file) return

  uploadError.value = ''
  result.value = null

  try {
    const response = await importMutation.mutateAsync(file)
    result.value = response
    toast.success(
      `Marked ${response.uniqueLines} line${response.uniqueLines === 1 ? '' : 's'} as voiced.`,
    )
  } catch (err) {
    uploadError.value = messageFromContentError(err)
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="space-y-1">
      <h2 class="text-sm font-semibold">Mark lines as voiced</h2>
      <p class="text-sm text-muted-foreground">
        Upload a sounds.json file. Every line with an audio file is set to
        <span class="font-medium">fixed</span>; lines that have no audio file are ignored.
      </p>
    </div>

    <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <div
        class="flex min-h-32 flex-col items-center justify-center gap-3 rounded-md border border-dashed p-6 text-center transition-colors"
        :class="{ 'border-primary bg-primary/5': isDragging }"
        @dragover.prevent="isDragging = true"
        @dragleave.prevent="isDragging = false"
        @drop.prevent="onDrop"
      >
        <Upload class="size-7 text-muted-foreground" />
        <p class="text-sm font-medium">Drop sounds.json here</p>
        <label>
          <input
            class="sr-only"
            type="file"
            accept=".json,application/json"
            @change="onFileInput"
          />
          <span
            class="inline-flex h-9 cursor-pointer items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow-xs"
          >
            Choose file
          </span>
        </label>
      </div>

      <div class="space-y-3">
        <div v-if="selectedFile" class="flex items-center gap-2 rounded-md border p-3 text-sm">
          <div class="min-w-0 flex-1">
            <div class="truncate font-mono text-xs">{{ selectedFile.name }}</div>
            <div class="text-xs text-muted-foreground">{{ formatBytes(selectedFile.size) }}</div>
          </div>
          <Badge :variant="isJson ? 'secondary' : 'destructive'">
            {{ isJson ? 'JSON' : 'Invalid' }}
          </Badge>
          <Button type="button" variant="ghost" size="icon-sm" :disabled="isUploading" @click="clearFile">
            <X class="size-4" />
          </Button>
        </div>
        <p v-else class="text-sm text-muted-foreground">No file selected.</p>

        <div class="flex flex-wrap gap-2">
          <Button type="button" :disabled="!selectedFile || !isJson || isUploading" @click="upload">
            {{ isUploading ? 'Importing…' : 'Import' }}
          </Button>
          <Button
            type="button"
            variant="outline"
            :disabled="!selectedFile || isUploading"
            @click="clearFile"
          >
            Clear
          </Button>
        </div>

        <p class="text-xs text-muted-foreground">
          The import is not transactional. If it fails partway through, simply upload the file again —
          re-running it is safe and no manual cleanup is needed.
        </p>
      </div>
    </div>

    <div
      v-if="uploadError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ uploadError }}
    </div>

    <div v-if="result" class="space-y-3">
      <div class="grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
        <div class="rounded-md border p-3">
          <div class="text-muted-foreground">Entries read</div>
          <div class="text-lg font-semibold">{{ result.totalEntries }}</div>
        </div>
        <div class="rounded-md border p-3">
          <div class="text-muted-foreground">Lines imported</div>
          <div class="text-lg font-semibold">{{ result.uniqueLines }}</div>
        </div>
        <div class="rounded-md border p-3">
          <div class="text-muted-foreground">Already reported</div>
          <div class="text-lg font-semibold">{{ result.alreadyPresent }}</div>
        </div>
        <div class="rounded-md border p-3">
          <div class="text-muted-foreground">Newly added</div>
          <div class="text-lg font-semibold">{{ result.rowsInserted }}</div>
        </div>
      </div>

      <div v-if="skipRows.length > 0" class="flex flex-wrap items-center gap-2 text-sm">
        <span class="text-muted-foreground">
          {{ result.skipped.total }} entr{{ result.skipped.total === 1 ? 'y' : 'ies' }} skipped:
        </span>
        <Badge v-for="row in skipRows" :key="row.label" variant="secondary">
          {{ row.value }} — {{ row.label.toLowerCase() }}
        </Badge>
      </div>
    </div>
  </section>
</template>
