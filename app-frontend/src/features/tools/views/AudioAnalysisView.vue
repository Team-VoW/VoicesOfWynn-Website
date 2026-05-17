<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  AlertTriangle,
  AudioLines,
  CheckCircle2,
  Download,
  Loader2,
  Upload,
  XCircle,
} from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { analyzeAudio, type AudioAnalysisItem } from '@/api/tools'
import { ApiError } from '@/api/client'

const LEADING_SILENCE_LIMIT = 0.1
const TRAILING_SILENCE_LIMIT = 0.4
const TRUE_PEAK_LIMIT = -1
const AUDIO_FILE_NAME_PATTERN = /^[a-z0-9]+-[a-z0-9]+-[1-9][0-9]*\.wav$/
const AUDIO_FILE_NAME_ERROR =
  'Filename must be questname-npcname-number.wav with only lowercase letters, numbers, exactly two hyphens, and no leading zeros in the number.'
const AUDACITY_MACRO_BASE_URL = `${import.meta.env.BASE_URL}audacity-macros/`

interface Row {
  id: number
  fileName: string
  status: 'analyzing' | 'done' | 'error'
  result?: AudioAnalysisItem
}

interface AudacityMacro {
  name: string
  fileName: string
  target?: string
  description: string
}

const volumeMacros: AudacityMacro[] = [
  {
    name: 'Volume to whispering volume',
    fileName: 'volume-to-whispering-volume.txt',
    target: '-23 LUFS',
    description: 'Use for fresh whisper recordings that should be quiet but still audible in-game.',
  },
  {
    name: 'Volume to speaking volume',
    fileName: 'volume-to-speaking-volume.txt',
    target: '-18 LUFS',
    description: 'Use for normal dialogue and most submitted voice lines.',
  },
  {
    name: 'Volume to shouting volume',
    fileName: 'volume-to-shouting-volume.txt',
    target: '-13 LUFS',
    description:
      'Use for intentionally loud lines such as combat calls, panic, or distant shouting that must cut through.',
  },
]

const exportVolumeMacros: AudacityMacro[] = [
  {
    name: 'Export volume to whispering volume',
    fileName: 'export-volume-to-whispering-volume.txt',
    target: '-23 LUFS',
    description:
      'Same whisper target, then exports WAV. Useful when batch processing many selected files.',
  },
  {
    name: 'Export volume to speaking volume',
    fileName: 'export-volume-to-speaking-volume.txt',
    target: '-18 LUFS',
    description:
      'Same speaking target, then exports WAV. Useful when batch processing the usual dialogue pass.',
  },
  {
    name: 'Export volume to shouting volume',
    fileName: 'export-volume-to-shouting-volume.txt',
    target: '-13 LUFS',
    description:
      'Same shouting target, then exports WAV. Useful when batch processing loud alternate takes.',
  },
]

const conversionMacros: AudacityMacro[] = [
  {
    name: 'Speaking to whispering',
    fileName: 'from-speaking-to-whispering-volume.txt',
    target: '-5 dB',
    description:
      'Use when a processed speaking line should become a whisper without running the full cleanup chain again.',
  },
  {
    name: 'Speaking to shouting',
    fileName: 'from-speaking-to-shouting-volume.txt',
    target: '+5 dB',
    description: 'Use when a processed speaking line should become a shouting variant.',
  },
  {
    name: 'Whispering to speaking',
    fileName: 'from-whispering-to-speaking-volume.txt',
    target: '+5 dB',
    description: 'Use when a quiet processed line needs to sit at normal dialogue level.',
  },
  {
    name: 'Whispering to shouting',
    fileName: 'from-whispering-to-shouting-volume.txt',
    target: '+10 dB',
    description:
      'Use sparingly; it can expose noise in quiet takes, but helps when the performance is right and only level is wrong.',
  },
  {
    name: 'Shouting to speaking',
    fileName: 'from-shouting-to-speaking-volume.txt',
    target: '-5 dB',
    description: 'Use when an edited loud line needs to return to normal dialogue loudness.',
  },
  {
    name: 'Shouting to whispering',
    fileName: 'from-shouting-to-whispering-volume.txt',
    target: '-10 dB',
    description:
      'Use for rough alternates only; a real whisper performance usually sounds better than lowering a shout.',
  },
]

const utilityMacros: AudacityMacro[] = [
  {
    name: 'Align end-to-end and mix',
    fileName: 'align-end-to-end-and-mix.txt',
    description:
      'Useful when selected clips should become one continuous line before rendering. Skip it for normal single-line volume fixes.',
  },
]

const rows = ref<Row[]>([])
const isDragging = ref(false)
const isAnalyzing = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
let nextId = 1

const summary = computed(() => {
  if (rows.value.length === 0)
    return 'Drop .wav files to analyze filenames, loudness, peak, silence, and channels.'
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

  const pendingRows: Row[] = wavs.map((f) => ({
    id: nextId++,
    fileName: f.name,
    status: 'analyzing',
  }))
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
        const fileNameError = validateAudioFileName(row.fileName)
        row.status = 'error'
        row.result = {
          fileName: row.fileName,
          success: false,
          fileNameValid: fileNameError === null,
          fileNameError,
          integratedLufs: null,
          maxTruePeakDbtp: null,
          leadingSilenceSeconds: null,
          trailingSilenceSeconds: null,
          channelMode: null,
          error: 'No result returned.',
        }
        continue
      }
      row.status = result.success ? 'done' : 'error'
      row.result = result
    }
  } catch (err) {
    const message =
      err instanceof ApiError ? `Analysis failed (HTTP ${err.status}).` : 'Analysis failed.'
    toast.error(message)
    for (const row of pendingRows) {
      const fileNameError = validateAudioFileName(row.fileName)
      row.status = 'error'
      row.result = {
        fileName: row.fileName,
        success: false,
        fileNameValid: fileNameError === null,
        fileNameError,
        integratedLufs: null,
        maxTruePeakDbtp: null,
        leadingSilenceSeconds: null,
        trailingSilenceSeconds: null,
        channelMode: null,
        error: message,
      }
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
  if (lufs < -25)
    return { label: 'very quiet — review unless intentionally subtle', tone: 'danger' }
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

function formatDbtp(value: number) {
  return `${value.toFixed(1)} dBTP`
}

function formatSeconds(value: number) {
  return `${value.toFixed(3)} s`
}

function formatChannelMode(value: AudioAnalysisItem['channelMode']) {
  if (value === 'mono') return 'Mono'
  if (value === 'stereo') return 'Stereo'
  return 'Unknown'
}

function validateAudioFileName(fileName: string) {
  return AUDIO_FILE_NAME_PATTERN.test(fileName) ? null : AUDIO_FILE_NAME_ERROR
}

function audacityMacroHref(fileName: string) {
  return `${AUDACITY_MACRO_BASE_URL}${fileName}`
}

function leadingOverLimit(seconds: number | null) {
  return seconds !== null && seconds > LEADING_SILENCE_LIMIT
}

function trailingOverLimit(seconds: number | null) {
  return seconds !== null && seconds > TRAILING_SILENCE_LIMIT
}

function truePeakOverLimit(dbtp: number | null) {
  return dbtp !== null && dbtp > TRUE_PEAK_LIMIT
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
        Drop WAV recordings to measure filenames, loudness, true peak, leading/trailing silence, and
        mono/stereo channels.
      </p>
    </header>

    <Card>
      <CardHeader class="gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle>Files</CardTitle>
          <CardDescription class="mt-1">{{ summary }}</CardDescription>
        </div>
        <Button v-if="rows.length > 0" variant="outline" :disabled="isAnalyzing" @click="clearAll"
          >Clear</Button
        >
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
            Targets: questname-npcname-number.wav, −23/−18/−13 LUFS, max −1 dBTP, mono.
          </p>
          <input
            ref="fileInput"
            type="file"
            accept=".wav,audio/wav,audio/wave,audio/x-wav"
            multiple
            class="hidden"
            @change="onFileInput"
          />
          <Button class="mt-4" :disabled="isAnalyzing" @click="fileInput?.click()">
            <Upload class="size-4" />
            Choose files
          </Button>
        </div>

        <ul v-if="rows.length > 0" class="space-y-3">
          <li v-for="row in rows" :key="row.id" class="rounded-md border bg-card p-4">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="truncate text-sm font-medium">{{ row.fileName }}</p>
                <p
                  v-if="row.status === 'analyzing'"
                  class="mt-1 inline-flex items-center gap-1.5 text-xs text-muted-foreground"
                >
                  <Loader2 class="size-3 animate-spin" />
                  Analyzing…
                </p>
                <p
                  v-else-if="row.status === 'error'"
                  class="mt-1 inline-flex items-center gap-1.5 text-xs text-destructive"
                >
                  <XCircle class="size-3" />
                  {{ row.result?.error ?? 'Failed.' }}
                </p>
                <p v-else class="mt-1 inline-flex items-center gap-1.5 text-xs text-emerald-700">
                  <CheckCircle2 class="size-3" />
                  Done
                </p>
              </div>
            </div>

            <div
              v-if="row.status === 'done' && row.result"
              class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6"
            >
              <div
                class="rounded-md border p-3 text-sm"
                :class="row.result.fileNameValid ? toneClass.success : toneClass.danger"
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Filename</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ row.result.fileNameValid ? 'Valid' : 'Invalid' }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="row.result.fileNameValid"
                    >matches questname-npcname-number.wav</template
                  >
                  <template v-else>{{ row.result.fileNameError }}</template>
                </div>
              </div>

              <div
                v-if="row.result.integratedLufs !== null"
                class="rounded-md border p-3 text-sm"
                :class="toneClass[lufsVerdict(row.result.integratedLufs).tone]"
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Integrated loudness</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ formatLufs(row.result.integratedLufs) }}
                </div>
                <div class="mt-1 text-xs">{{ lufsVerdict(row.result.integratedLufs).label }}</div>
              </div>

              <div
                v-if="row.result.maxTruePeakDbtp !== null"
                class="rounded-md border p-3 text-sm"
                :class="
                  truePeakOverLimit(row.result.maxTruePeakDbtp)
                    ? toneClass.danger
                    : toneClass.success
                "
              >
                <div class="text-xs uppercase tracking-wide opacity-70">True peak</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ formatDbtp(row.result.maxTruePeakDbtp) }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="truePeakOverLimit(row.result.maxTruePeakDbtp)">
                    above max {{ formatDbtp(TRUE_PEAK_LIMIT) }} — reduce peak level
                  </template>
                  <template v-else>within max {{ formatDbtp(TRUE_PEAK_LIMIT) }}</template>
                </div>
              </div>

              <div
                class="rounded-md border p-3 text-sm"
                :class="
                  row.result.channelMode === 'stereo'
                    ? toneClass.danger
                    : row.result.channelMode === 'mono'
                      ? toneClass.success
                      : 'border-border bg-muted/30 text-foreground'
                "
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Channels</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{ formatChannelMode(row.result.channelMode) }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="row.result.channelMode === 'stereo'">stereo detected</template>
                  <template v-else-if="row.result.channelMode === 'mono'"
                    >correct mono format</template
                  >
                  <template v-else>channel count unavailable</template>
                </div>
              </div>

              <div
                class="rounded-md border p-3 text-sm"
                :class="
                  leadingOverLimit(row.result.leadingSilenceSeconds)
                    ? toneClass.danger
                    : toneClass.success
                "
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Leading silence</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{
                    row.result.leadingSilenceSeconds !== null
                      ? formatSeconds(row.result.leadingSilenceSeconds)
                      : '—'
                  }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="leadingOverLimit(row.result.leadingSilenceSeconds)">
                    over the {{ LEADING_SILENCE_LIMIT.toFixed(2) }} s limit — trim the head
                  </template>
                  <template v-else
                    >within limit (≤ {{ LEADING_SILENCE_LIMIT.toFixed(2) }} s)</template
                  >
                </div>
              </div>

              <div
                class="rounded-md border p-3 text-sm"
                :class="
                  trailingOverLimit(row.result.trailingSilenceSeconds)
                    ? toneClass.danger
                    : toneClass.success
                "
              >
                <div class="text-xs uppercase tracking-wide opacity-70">Trailing silence</div>
                <div class="mt-0.5 text-base font-semibold">
                  {{
                    row.result.trailingSilenceSeconds !== null
                      ? formatSeconds(row.result.trailingSilenceSeconds)
                      : '—'
                  }}
                </div>
                <div class="mt-1 text-xs">
                  <template v-if="trailingOverLimit(row.result.trailingSilenceSeconds)">
                    over the {{ TRAILING_SILENCE_LIMIT.toFixed(2) }} s limit — trim the tail
                  </template>
                  <template v-else
                    >within limit (≤ {{ TRAILING_SILENCE_LIMIT.toFixed(2) }} s)</template
                  >
                </div>
              </div>
            </div>

            <div
              v-if="row.result?.fileNameValid === false"
              class="mt-3 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
            >
              <AlertTriangle class="mt-0.5 size-4 shrink-0" />
              <span>{{ row.result.fileNameError }}</span>
            </div>

            <div
              v-if="
                row.status === 'done' &&
                row.result?.integratedLufs !== null &&
                row.result!.integratedLufs! < -25
              "
              class="mt-3 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
            >
              <AlertTriangle class="mt-0.5 size-4 shrink-0" />
              <span
                >This clip is under −25 LUFS. Usually that means the recording is too quiet, but
                short intentional sounds like “hm”, breaths, or murmurs may be okay if they sound
                right in-game.</span
              >
            </div>

            <div
              v-if="row.status === 'done' && truePeakOverLimit(row.result?.maxTruePeakDbtp ?? null)"
              class="mt-3 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
            >
              <AlertTriangle class="mt-0.5 size-4 shrink-0" />
              <span
                >This clip peaks above {{ formatDbtp(TRUE_PEAK_LIMIT) }}. Lower the gain or limiter
                ceiling so the exported file stays below the mod import target.</span
              >
            </div>

            <div
              v-if="row.status === 'done' && row.result?.channelMode === 'stereo'"
              class="mt-3 flex items-start gap-3 rounded-md border border-destructive/50 bg-destructive/15 p-4 text-base font-semibold text-destructive"
            >
              <AlertTriangle class="mt-0.5 size-5 shrink-0" />
              <span>This file is stereo. Voices of Wynn recordings should be mono.</span>
            </div>
          </li>
        </ul>

        <div
          v-else-if="!isAnalyzing"
          class="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground"
        >
          No results yet.
        </div>
      </CardContent>
    </Card>

    <Card>
      <CardHeader>
        <CardTitle>Audacity macros</CardTitle>
        <CardDescription>
          Download these into Audacity's macros folder or import them from Tools → Macros. The
          export versions are for batch processing because they normalize and export WAV in one run.
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <section class="space-y-3">
          <div>
            <h2 class="text-sm font-semibold">Normalize raw volume</h2>
            <p class="mt-1 text-sm text-muted-foreground">
              Use these when the file needs the full baseline pass: mono conversion, high-pass
              filtering, loudness normalization, and limiting.
            </p>
          </div>
          <div class="grid gap-3 lg:grid-cols-3">
            <div
              v-for="macro in volumeMacros"
              :key="macro.fileName"
              class="rounded-md border bg-muted/30 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <h3 class="text-sm font-medium">{{ macro.name }}</h3>
                  <Badge v-if="macro.target" variant="secondary" class="mt-2">{{
                    macro.target
                  }}</Badge>
                </div>
                <Button
                  as="a"
                  :href="audacityMacroHref(macro.fileName)"
                  :download="macro.fileName"
                  variant="outline"
                  size="sm"
                >
                  <Download class="size-4" />
                  Download
                </Button>
              </div>
              <p class="mt-3 text-sm text-muted-foreground">{{ macro.description }}</p>
            </div>
          </div>
        </section>

        <section class="space-y-3">
          <div>
            <h2 class="text-sm font-semibold">Normalize and export</h2>
            <p class="mt-1 text-sm text-muted-foreground">
              Use these for Audacity batch processing when each input should be normalized to a
              target and written back out as WAV.
            </p>
          </div>
          <div class="grid gap-3 lg:grid-cols-3">
            <div
              v-for="macro in exportVolumeMacros"
              :key="macro.fileName"
              class="rounded-md border bg-muted/30 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <h3 class="text-sm font-medium">{{ macro.name }}</h3>
                  <Badge v-if="macro.target" variant="secondary" class="mt-2">{{
                    macro.target
                  }}</Badge>
                </div>
                <Button
                  as="a"
                  :href="audacityMacroHref(macro.fileName)"
                  :download="macro.fileName"
                  variant="outline"
                  size="sm"
                >
                  <Download class="size-4" />
                  Download
                </Button>
              </div>
              <p class="mt-3 text-sm text-muted-foreground">{{ macro.description }}</p>
            </div>
          </div>
        </section>

        <section class="space-y-3">
          <div>
            <h2 class="text-sm font-semibold">Convert between processed volumes</h2>
            <p class="mt-1 text-sm text-muted-foreground">
              Use these after a line has already been cleaned up and only needs to move between
              whispering, speaking, and shouting loudness.
            </p>
          </div>
          <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <div
              v-for="macro in conversionMacros"
              :key="macro.fileName"
              class="rounded-md border bg-muted/30 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <div>
                  <h3 class="text-sm font-medium">{{ macro.name }}</h3>
                  <Badge v-if="macro.target" variant="secondary" class="mt-2">{{
                    macro.target
                  }}</Badge>
                </div>
                <Button
                  as="a"
                  :href="audacityMacroHref(macro.fileName)"
                  :download="macro.fileName"
                  variant="outline"
                  size="sm"
                >
                  <Download class="size-4" />
                  Download
                </Button>
              </div>
              <p class="mt-3 text-sm text-muted-foreground">{{ macro.description }}</p>
            </div>
          </div>
        </section>

        <section class="space-y-3">
          <div>
            <h2 class="text-sm font-semibold">Utility</h2>
          </div>
          <div class="grid gap-3 lg:grid-cols-3">
            <div
              v-for="macro in utilityMacros"
              :key="macro.fileName"
              class="rounded-md border bg-muted/30 p-4"
            >
              <div class="flex items-start justify-between gap-3">
                <h3 class="text-sm font-medium">{{ macro.name }}</h3>
                <Button
                  as="a"
                  :href="audacityMacroHref(macro.fileName)"
                  :download="macro.fileName"
                  variant="outline"
                  size="sm"
                >
                  <Download class="size-4" />
                  Download
                </Button>
              </div>
              <p class="mt-3 text-sm text-muted-foreground">{{ macro.description }}</p>
            </div>
          </div>
        </section>
      </CardContent>
    </Card>

    <Card>
      <CardHeader>
        <CardTitle>Reference</CardTitle>
      </CardHeader>
      <CardContent class="space-y-5">
        <ul class="grid gap-2 text-sm sm:grid-cols-2">
          <li class="flex items-center gap-2">
            <Badge variant="secondary">questname-npcname-number.wav</Badge> required filename
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="secondary">−23 LUFS</Badge> whispering
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="secondary">−18 LUFS</Badge> speaking (default target)
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="secondary">−13 LUFS</Badge> shouting
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="secondary">≤ −1 dBTP</Badge> max true peak
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="secondary">Mono</Badge> required channel format
          </li>
          <li class="flex items-center gap-2">
            <Badge variant="destructive">&lt; −25 LUFS</Badge> review quiet clips
          </li>
        </ul>
        <div class="grid gap-3 text-sm lg:grid-cols-3">
          <div class="rounded-md border bg-muted/30 p-4">
            <h2 class="font-medium">Filename format</h2>
            <p class="mt-1 text-muted-foreground">
              Export files as
              <span class="font-mono text-foreground">questname-npcname-number.wav</span>. The base
              name must use lowercase letters and numbers only, with exactly two hyphens separating
              quest, NPC, and line number. Use <span class="font-mono text-foreground">1</span>,
              <span class="font-mono text-foreground">2</span>,
              <span class="font-mono text-foreground">3</span>, not
              <span class="font-mono text-foreground">001</span>.
            </p>
          </div>
          <div class="rounded-md border bg-muted/30 p-4">
            <h2 class="font-medium">Loudness and peak</h2>
            <p class="mt-1 text-muted-foreground">
              LUFS is the perceived average loudness target: quieter whispers near −23, normal
              speech near −18, and shouting near −13. True peak catches short peaks between samples;
              keep it at or below −1 dBTP so exported audio has headroom.
            </p>
          </div>
          <div class="rounded-md border bg-muted/30 p-4">
            <h2 class="font-medium">Mono audio</h2>
            <p class="mt-1 text-muted-foreground">
              Voice lines should be mono. Stereo files add unnecessary size and can behave
              differently in-game, so convert the export to one channel before submitting.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
