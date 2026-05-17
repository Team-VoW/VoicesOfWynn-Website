<script setup lang="ts">
import { computed, ref } from 'vue'
import { AlertTriangle, AudioLines, CheckCircle2, Loader2, Upload, XCircle } from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { analyzeAudio, type AudioAnalysisItem } from '@/api/tools'
import { ApiError } from '@/api/client'

const LEADING_SILENCE_LIMIT = 0.1
const TRAILING_SILENCE_LIMIT = 0.4

interface Row {
  id: number
  fileName: string
  status: 'analyzing' | 'done' | 'error'
  result?: AudioAnalysisItem
}

const rows = ref<Row[]>([])
const isDragging = ref(false)
const isAnalyzing = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
let nextId = 1

const summary = computed(() => {
  if (rows.value.length === 0) return 'Drop .wav files to analyze loudness and silence.'
  const done = rows.value.filter((r) => r.status === 'done').length
  const errored = rows.value.filter((r) => r.status === 'error').length
  if (isAnalyzing.value) return `Analyzing ${rows.value.length} file(s)…`
  return `${done} analyzed, ${errored} failed.`
})

function onDrop(event: DragEvent) {
  isDragging.value = false
  const files = Array.from(event.dataTransfer?.files ?? [])
  void submit(files)
}

function onFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  void submit(files)
}

async function submit(allFiles: File[]) {
  const wavs = allFiles.filter((f) => f.name.toLowerCase().endsWith('.wav'))
  if (allFiles.length === 0) return
  if (wavs.length === 0) {
    toast.error('Only .wav files are accepted.')
    return
  }
  if (wavs.length !== allFiles.length) {
    toast.warning(`Skipped ${allFiles.length - wavs.length} non-.wav file(s).`)
  }

  const pendingRows: Row[] = wavs.map((f) => ({ id: nextId++, fileName: f.name, status: 'analyzing' }))
  rows.value = [...pendingRows, ...rows.value]
  isAnalyzing.value = true

  try {
    const response = await analyzeAudio(wavs)
    // Match results to pendingRows by index (server preserves order).
    for (let i = 0; i < pendingRows.length; i++) {
      const row = pendingRows[i]
      if (!row) continue
      const result = response.results[i]
      if (!result) {
        row.status = 'error'
        row.result = { fileName: row.fileName, success: false, integratedLufs: null, leadingSilenceSeconds: null, trailingSilenceSeconds: null, error: 'No result returned.' }
        continue
      }
      row.status = result.success ? 'done' : 'error'
      row.result = result
    }
  } catch (err) {
    const message = err instanceof ApiError ? `Analysis failed (HTTP ${err.status}).` : 'Analysis failed.'
    toast.error(message)
    for (const row of pendingRows) {
      row.status = 'error'
      row.result = { fileName: row.fileName, success: false, integratedLufs: null, leadingSilenceSeconds: null, trailingSilenceSeconds: null, error: message }
    }
  } finally {
    isAnalyzing.value = false
    rows.value = [...rows.value]
  }
}

function clearAll() {
  rows.value = []
}

interface LufsVerdict {
  label: string
  tone: 'danger' | 'warning' | 'success' | 'info'
}

function lufsVerdict(lufs: number): LufsVerdict {
  if (lufs < -25) return { label: 'way too quiet — likely a recording problem', tone: 'danger' }
  if (lufs < -22) return { label: 'whispering — on the quiet side', tone: 'info' }
  if (lufs < -20) return { label: 'whispering — perfect (~-23)', tone: 'success' }
  if (lufs < -19) return { label: 'speaking — a touch quiet', tone: 'info' }
  if (lufs < -17) return { label: 'speaking — perfect (~-18)', tone: 'success' }
  if (lufs < -16) return { label: 'speaking — a bit louder', tone: 'info' }
  if (lufs < -14) return { label: 'speaking — LOUD, watch headroom', tone: 'warning' }
  if (lufs < -12) return { label: 'shouting — perfect (~-13)', tone: 'success' }
  return { label: 'very loud — likely too hot for shouting', tone: 'warning' }
}

function formatLufs(value: number) {
  return `${value.toFixed(1)} LUFS`
}

function formatSeconds(value: number) {
  return `${value.toFixed(3)} s`
}

function leadingOverLimit(seconds: number | null) {
  return seconds !== null && seconds > LEADING_SILENCE_LIMIT
}

function trailingOverLimit(seconds: number | null) {
  return seconds !== null && seconds > TRAILING_SILENCE_LIMIT
}

const toneClass: Record<LufsVerdict['tone'], string> = {
  danger: 'border-destructive/40 bg-destructive/10 text-destructive',
  warning: 'border-amber-300 bg-amber-50 text-amber-900',
  success: 'border-emerald-300 bg-emerald-50 text-emerald-900',
  info: 'border-sky-300 bg-sky-50 text-sky-900',
}
</script>

<template>
  <div class="mx-auto max-w-screen-xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Audio check</h1>
      <p class="text-sm text-muted-foreground">
        Drop WAV recordings to measure integrated loudness (LUFS) and flag leading/trailing silence.
      </p>
    </header>

    <Card>
      <CardHeader class="gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle>Files</CardTitle>
          <CardDescription class="mt-1">{{ summary }}</CardDescription>
        </div>
        <Button v-if="rows.length > 0" variant="outline" :disabled="isAnalyzing" @click="clearAll">Clear</Button>
      </CardHeader>
      <CardContent class="space-y-5">
        <div
          class="rounded-md border-2 border-dashed p-8 text-center transition-colors"
          :class="isDragging ? 'border-primary bg-primary/5' : 'border-border bg-muted/40'"
          @dragenter.prevent="isDragging = true"
          @dragover.prevent="isDragging = true"
          @dragleave.prevent="isDragging = false"
          @drop.prevent="onDrop"
        >
          <AudioLines class="mx-auto size-10 text-primary" />
          <p class="mt-3 text-sm font-medium">Drag .wav files here</p>
          <p class="mt-1 text-xs text-muted-foreground">
            Targets: −23 LUFS whispering, −18 speaking, −13 shouting. Anything below −25 LUFS gets flagged.
          </p>
          <input
            ref="fileInput"
            type="file"
            accept=".wav,audio/wav,audio/wave,audio/x-wav"
            multiple
            class="hidden"
            @change="onFileInput"
          >
          <Button class="mt-4" :disabled="isAnalyzing" @click="fileInput?.click()">
            <Upload class="size-4" />
            Choose files
          </Button>
        </div>

        <ul v-if="rows.length > 0" class="space-y-3">
          <li
            v-for="row in rows"
            :key="row.id"
            class="rounded-md border bg-card p-4"
          >
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="truncate text-sm font-medium">{{ row.fileName }}</p>
                <p v-if="row.status === 'analyzing'" class="mt-1 inline-flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Loader2 class="size-3 animate-spin" />
                  Analyzing…
                </p>
                <p v-else-if="row.status === 'error'" class="mt-1 inline-flex items-center gap-1.5 text-xs text-destructive">
                  <XCircle class="size-3" />
                  {{ row.result?.error ?? 'Failed.' }}
                </p>
                <p v-else class="mt-1 inline-flex items-center gap-1.5 text-xs text-emerald-700">
                  <CheckCircle2 class="size-3" />
                  Done
                </p>
              </div>
            </div>

            <div v-if="row.status === 'done' && row.result" class="mt-4 grid gap-3 sm:grid-cols-3">
              <div
                v-if="row.result.integratedLufs !== null"
                class="rounded-md border p-3 text-sm"
                :class="toneClass[lufsVerdict(row.result.integratedLufs).tone]"
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Integrated loudness</div>
                <div class="mt-0.5 text-base font-semibold">{{ formatLufs(row.result.integratedLufs) }}</div>
                <div class="mt-1 text-xs">{{ lufsVerdict(row.result.integratedLufs).label }}</div>
              </div>

              <div
                class="rounded-md border p-3 text-sm"
                :class="leadingOverLimit(row.result.leadingSilenceSeconds) ? toneClass.danger : 'border-border bg-muted/30 text-foreground'"
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Leading silence</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ row.result.leadingSilenceSeconds !== null ? formatSeconds(row.result.leadingSilenceSeconds) : '—' }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="leadingOverLimit(row.result.leadingSilenceSeconds)">
                    over the {{ LEADING_SILENCE_LIMIT.toFixed(2) }} s limit — trim the head
                  </template>
                  <template v-else>within limit (≤ {{ LEADING_SILENCE_LIMIT.toFixed(2) }} s)</template>
                </div>
              </div>

              <div
                class="rounded-md border p-3 text-sm"
                :class="trailingOverLimit(row.result.trailingSilenceSeconds) ? toneClass.danger : 'border-border bg-muted/30 text-foreground'"
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Trailing silence</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ row.result.trailingSilenceSeconds !== null ? formatSeconds(row.result.trailingSilenceSeconds) : '—' }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="trailingOverLimit(row.result.trailingSilenceSeconds)">
                    over the {{ TRAILING_SILENCE_LIMIT.toFixed(2) }} s limit — trim the tail
                  </template>
                  <template v-else>within limit (≤ {{ TRAILING_SILENCE_LIMIT.toFixed(2) }} s)</template>
                </div>
              </div>
            </div>

            <div
              v-if="row.status === 'done' && row.result?.integratedLufs !== null && row.result!.integratedLufs! < -25"
              class="mt-3 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
            >
              <AlertTriangle class="mt-0.5 size-4 shrink-0" />
              <span>This clip is under −25 LUFS — almost certainly a recording problem (mic too far, gain too low, or wrong source).</span>
            </div>
          </li>
        </ul>

        <div v-else-if="!isAnalyzing" class="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          No results yet.
        </div>
      </CardContent>
    </Card>

    <Card>
      <CardHeader>
        <CardTitle>Reference</CardTitle>
      </CardHeader>
      <CardContent>
        <ul class="grid gap-2 text-sm sm:grid-cols-2">
          <li class="flex items-center gap-2"><Badge variant="secondary">−23 LUFS</Badge> whispering</li>
          <li class="flex items-center gap-2"><Badge variant="secondary">−18 LUFS</Badge> speaking (default target)</li>
          <li class="flex items-center gap-2"><Badge variant="secondary">−13 LUFS</Badge> shouting</li>
          <li class="flex items-center gap-2"><Badge variant="destructive">&lt; −25 LUFS</Badge> almost certainly broken</li>
        </ul>
      </CardContent>
    </Card>
  </div>
</template>
