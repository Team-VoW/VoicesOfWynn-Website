<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  AlertTriangle,
  CheckCircle2,
  ChevronDown,
  Clipboard,
  Code2,
  Copy,
  File,
  FileText,
  FolderOpen,
  Info,
  Music,
  RefreshCw,
  Upload,
} from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import {
  compareAudioFiles,
  formatGeneratedOutput,
  generateCodeJsonOutput,
  generateCodeLines,
  getSelectedAudioFileNames,
  parseScriptText,
  type ParsedVoiceLine,
  type SelectedAudioFileNames,
} from '../lib/scriptTools'

const questName = ref('')
const rawScriptText = ref('')
const selectedFiles = ref<string[]>([])
const voiceLines = ref<ParsedVoiceLine[]>([])
const selectedNpc = ref<string | null>(null)
const generateFileNames = ref(false)
const stripLineInformation = ref(false)
const sourceLineCount = ref(0)
const skippedLineCount = ref(0)
const message = ref('')
const messageTone = ref<'error' | 'success' | 'neutral'>('neutral')
const isDragging = ref(false)
const isAudioDragging = ref(false)
const isCodeOutputOpen = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const audioFolderInput = ref<HTMLInputElement | null>(null)
const audioFilesInput = ref<HTMLInputElement | null>(null)
const audioFileNames = ref<SelectedAudioFileNames | null>(null)

const npcOptions = computed(() => {
  const seen = new Set<string>()
  return voiceLines.value
    .map((line) => line.speaker)
    .filter((speaker) => {
      if (seen.has(speaker)) return false
      seen.add(speaker)
      return true
    })
})

const filteredVoiceLines = computed(() =>
  selectedNpc.value === null
    ? voiceLines.value
    : voiceLines.value.filter((line) => line.speaker === selectedNpc.value),
)

const outputSummary = computed(() => {
  if (voiceLines.value.length === 0) return 'No generated lines yet.'
  if (selectedNpc.value !== null) {
    return `${filteredVoiceLines.value.length} of ${voiceLines.value.length} lines for ${selectedNpc.value}.`
  }
  return `${voiceLines.value.length} generated, ${skippedLineCount.value} skipped, ${sourceLineCount.value} source lines.`
})

const codeLines = computed(() => generateCodeLines(rawScriptText.value, questName.value))
const codeJsonOutput = computed(() => generateCodeJsonOutput(rawScriptText.value, questName.value))
const codeOutputSummary = computed(() =>
  codeLines.value.length === 0 ? 'No JSON generated yet.' : `${codeLines.value.length} JSON entries ready.`,
)

const audioComparison = computed(() =>
  compareAudioFiles(
    voiceLines.value.map((line) => line.fileName),
    audioFileNames.value?.fileNames ?? [],
  ),
)

const audioCheckPassed = computed(() =>
  audioComparison.value.expectedCount > 0 &&
  audioComparison.value.actualCount > 0 &&
  audioComparison.value.missingFileNames.length === 0 &&
  audioComparison.value.extraFileNames.length === 0,
)

const audioCheckStatus = computed(() => {
  if (audioComparison.value.expectedCount === 0) {
    return 'Add a script file first so the checker knows which audio files to expect.'
  }
  if (audioComparison.value.actualCount === 0) return 'No .ogg or .wav files were found in the selection.'
  if (audioCheckPassed.value) return 'All generated filenames are present and no extra audio files were found.'
  return `${audioComparison.value.missingFileNames.length} missing and ${audioComparison.value.extraFileNames.length} extra audio filenames found.`
})

const messageClass = computed(() => ({
  'border-destructive/50 bg-destructive/5 text-destructive': messageTone.value === 'error',
  'border-emerald-200 bg-emerald-50 text-emerald-800': messageTone.value === 'success',
  'border-border bg-muted text-muted-foreground': messageTone.value === 'neutral',
}))

async function handleDrop(event: DragEvent) {
  isDragging.value = false
  await processFiles(Array.from(event.dataTransfer?.files ?? []))
}

async function handleFileInput(event: Event) {
  const input = event.target as HTMLInputElement
  await processFiles(Array.from(input.files ?? []))
  input.value = ''
}

function handleAudioDrop(event: DragEvent) {
  isAudioDragging.value = false
  processAudioFiles(Array.from(event.dataTransfer?.files ?? []))
}

function handleAudioInput(event: Event) {
  const input = event.target as HTMLInputElement
  processAudioFiles(Array.from(input.files ?? []))
  input.value = ''
}

async function processFiles(files: File[]) {
  const textFiles = files.filter((file) => file.name.toLowerCase().endsWith('.txt'))
  if (files.length === 0) return

  if (textFiles.length !== files.length) {
    setMessage('Only .txt script files can be processed.', 'error')
  }

  if (textFiles.length === 0) {
    selectedFiles.value = []
    voiceLines.value = []
    selectedNpc.value = null
    rawScriptText.value = ''
    sourceLineCount.value = 0
    skippedLineCount.value = 0
    return
  }

  rawScriptText.value = (await Promise.all(textFiles.map((file) => file.text()))).join('\n')
  const firstTextFile = textFiles[0]
  if (questName.value.trim() === '' && firstTextFile) {
    questName.value = firstTextFile.name.replace(/\.[^/.]+$/, '')
  }

  selectedFiles.value = textFiles.map((file) => file.name)
  selectedNpc.value = null
  regenerateVoiceLines()

  setMessage(
    voiceLines.value.length === 0
      ? 'No voice lines were found in the selected script files.'
      : `Generated ${voiceLines.value.length} voice lines.`,
    voiceLines.value.length === 0 ? 'neutral' : 'success',
  )
}

function processAudioFiles(files: File[]) {
  if (files.length === 0) return
  audioFileNames.value = getSelectedAudioFileNames(files)

  setMessage(
    audioFileNames.value.fileNames.length === 0
      ? 'No .ogg or .wav files were found in the selected audio files.'
      : `Checked ${audioFileNames.value.fileNames.length} audio filenames.`,
    audioFileNames.value.fileNames.length === 0 ? 'neutral' : 'success',
  )
}

function regenerateVoiceLines() {
  const result = parseScriptText(rawScriptText.value, questName.value, {
    stripLineInformation: stripLineInformation.value,
  })
  voiceLines.value = result.voiceLines
  sourceLineCount.value = result.sourceLineCount
  skippedLineCount.value = result.skippedLineCount

  if (selectedNpc.value !== null && !npcOptions.value.includes(selectedNpc.value)) selectedNpc.value = null
}

function handleRegenerate() {
  if (rawScriptText.value === '') return
  regenerateVoiceLines()
  setMessage(`Regenerated ${voiceLines.value.length} voice lines.`, 'success')
}

function handleStripLineInformationChange() {
  if (rawScriptText.value !== '') regenerateVoiceLines()
}

async function copyOutput() {
  await copyText(formatGeneratedOutput(filteredVoiceLines.value, { includeFileNames: generateFileNames.value }))
}

async function copyCodeJson() {
  await copyText(codeJsonOutput.value)
}

async function copyAudioList(fileNames: string[]) {
  await copyText(fileNames.join('\n'))
}

async function copyText(text: string) {
  await navigator.clipboard.writeText(text)
  setMessage('Copied to clipboard.', 'success')
}

function setMessage(text: string, tone: 'error' | 'success' | 'neutral') {
  message.value = text
  messageTone.value = tone
}
</script>

<template>
  <div class="mx-auto max-w-screen-xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Script tools</h1>
      <p class="text-sm text-muted-foreground">
        Generate voice-line filenames, code JSON, and compare expected audio filenames.
      </p>
    </header>

    <div class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
      <div class="min-w-0 space-y-5">
        <Card>
          <CardHeader>
            <CardTitle>Script input</CardTitle>
            <CardDescription>Files are processed locally in your browser.</CardDescription>
          </CardHeader>
          <CardContent class="space-y-5">
            <div class="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
              <label class="space-y-2">
                <span class="text-sm font-medium">Quest name</span>
                <Input v-model="questName" placeholder="A Grave Mistake" />
              </label>
              <Button variant="outline" :disabled="rawScriptText === ''" @click="handleRegenerate">
                <RefreshCw class="size-4" />
                Regenerate
              </Button>
            </div>

            <div
              class="rounded-md border-2 border-dashed p-8 text-center transition-colors"
              :class="isDragging ? 'border-primary bg-primary/5' : 'border-border bg-muted/40'"
              @dragenter.prevent="isDragging = true"
              @dragover.prevent="isDragging = true"
              @dragleave.prevent="isDragging = false"
              @drop.prevent="handleDrop"
            >
              <FileText class="mx-auto size-10 text-primary" />
              <p class="mt-3 text-sm font-medium">Drag script .txt files here</p>
              <p class="mt-1 text-xs text-muted-foreground">Multiple files are combined in selection order.</p>
              <input
                ref="fileInput"
                type="file"
                accept=".txt,text/plain"
                multiple
                class="hidden"
                @change="handleFileInput"
              >
              <Button class="mt-4" @click="fileInput?.click()">
                <Upload class="size-4" />
                Choose files
              </Button>
            </div>

            <div v-if="selectedFiles.length > 0" class="flex flex-wrap gap-2">
              <Badge v-for="file in selectedFiles" :key="file" variant="secondary">
                <File class="mr-1 size-3" />
                {{ file }}
              </Badge>
            </div>

            <div v-if="message" class="rounded-md border p-3 text-sm" :class="messageClass">
              {{ message }}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader class="gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <CardTitle>Generated voice lines</CardTitle>
              <CardDescription class="mt-1">{{ outputSummary }}</CardDescription>
            </div>
            <div class="flex flex-col gap-2 sm:items-end">
              <div class="flex flex-wrap gap-4 text-sm">
                <label class="inline-flex items-center gap-2">
                  <input v-model="generateFileNames" type="checkbox" class="size-4 rounded border-input">
                  Generate file names
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <button
                        type="button"
                        class="rounded-full text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        aria-label="Generate file names help"
                      >
                        <Info class="size-3.5" />
                      </button>
                    </TooltipTrigger>
                    <TooltipContent side="top" class="max-w-72">
                      Shows generated filenames in the preview and includes them when copying output.
                    </TooltipContent>
                  </Tooltip>
                </label>
                <label class="inline-flex items-center gap-2">
                  <input
                    v-model="stripLineInformation"
                    type="checkbox"
                    class="size-4 rounded border-input"
                    @change="handleStripLineInformationChange"
                  >
                  Strip information
                  <Tooltip>
                    <TooltipTrigger as-child>
                      <button
                        type="button"
                        class="rounded-full text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        aria-label="Strip information help"
                      >
                        <Info class="size-3.5" />
                      </button>
                    </TooltipTrigger>
                    <TooltipContent side="top" class="max-w-80">
                      Removes inline notes in braces, trailing // comments, and character-change markers from generated dialogue.
                    </TooltipContent>
                  </Tooltip>
                </label>
              </div>
              <Button
                variant="outline"
                size="sm"
                :disabled="filteredVoiceLines.length === 0"
                @click="copyOutput"
              >
                <Copy class="size-4" />
                Copy output
              </Button>
            </div>
          </CardHeader>
          <CardContent class="space-y-4">
            <div v-if="voiceLines.length === 0" class="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              Add a quest name and drop a script file to generate voice lines.
            </div>

            <template v-else>
              <div class="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  :variant="selectedNpc === null ? 'default' : 'outline'"
                  @click="selectedNpc = null"
                >
                  All NPCs
                </Button>
                <Button
                  v-for="npc in npcOptions"
                  :key="npc"
                  size="sm"
                  :variant="selectedNpc === npc ? 'default' : 'outline'"
                  @click="selectedNpc = npc"
                >
                  {{ npc }}
                </Button>
              </div>

              <div v-if="filteredVoiceLines.length === 0" class="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
                No generated lines match the selected NPC.
              </div>

              <div v-else class="max-h-[620px] overflow-auto rounded-md border">
                <div
                  v-for="(line, index) in filteredVoiceLines"
                  :key="`${line.fileName}-${index}`"
                  class="grid gap-3 border-b p-4 last:border-b-0"
                  :class="generateFileNames ? 'lg:grid-cols-[minmax(0,1fr)_260px]' : ''"
                >
                  <div class="min-w-0">
                    <p class="text-xs font-medium text-muted-foreground">#{{ index + 1 }} · {{ line.speaker }}</p>
                    <p class="mt-1 break-words text-sm">{{ line.cleanedLine }}</p>
                  </div>
                  <div v-if="generateFileNames" class="flex min-w-0 items-start gap-2 lg:justify-end">
                    <code class="min-w-0 flex-1 rounded-md bg-muted px-2 py-1.5 text-xs lg:text-right">
                      {{ line.fileName }}
                    </code>
                    <Button
                      variant="outline"
                      size="icon-sm"
                      :aria-label="`Copy ${line.fileName}`"
                      @click="copyText(line.fileName)"
                    >
                      <Copy class="size-4" />
                    </Button>
                  </div>
                </div>
              </div>
            </template>
          </CardContent>
        </Card>

        <Card>
          <CardHeader class="gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <CardTitle class="flex items-center gap-2">
                <Music class="size-4 text-primary" />
                File checker
              </CardTitle>
              <CardDescription class="mt-1">Compare generated filenames with selected local audio files.</CardDescription>
            </div>
            <p class="text-sm text-muted-foreground">
              {{ audioComparison.missingFileNames.length }} missing, {{ audioComparison.extraFileNames.length }} extra
            </p>
          </CardHeader>
          <CardContent class="space-y-5">
            <div
              class="rounded-md border-2 border-dashed p-6 text-center transition-colors"
              :class="isAudioDragging ? 'border-primary bg-primary/5' : 'border-border bg-muted/40'"
              @dragenter.prevent="isAudioDragging = true"
              @dragover.prevent="isAudioDragging = true"
              @dragleave.prevent="isAudioDragging = false"
              @drop.prevent="handleAudioDrop"
            >
              <FolderOpen class="mx-auto size-9 text-primary" />
              <p class="mt-3 text-sm font-medium">Select an audio folder or audio files</p>
              <p class="mt-1 text-xs text-muted-foreground">Only filenames are used.</p>
              <input ref="audioFolderInput" type="file" webkitdirectory multiple class="hidden" @change="handleAudioInput">
              <input
                ref="audioFilesInput"
                type="file"
                accept=".ogg,.wav,audio/ogg,audio/wav"
                multiple
                class="hidden"
                @change="handleAudioInput"
              >
              <div class="mt-4 flex flex-col justify-center gap-2 sm:flex-row">
                <Button @click="audioFolderInput?.click()">
                  <FolderOpen class="size-4" />
                  Choose folder
                </Button>
                <Button variant="outline" @click="audioFilesInput?.click()">
                  <Upload class="size-4" />
                  Choose files
                </Button>
              </div>
            </div>

            <div v-if="audioFileNames !== null" class="space-y-4">
              <div class="grid gap-3 sm:grid-cols-3">
                <div class="rounded-md border bg-muted/30 p-3">
                  <p class="text-xs font-medium uppercase text-muted-foreground">Expected</p>
                  <p class="mt-1 text-lg font-semibold">{{ audioComparison.expectedCount }}</p>
                </div>
                <div class="rounded-md border bg-muted/30 p-3">
                  <p class="text-xs font-medium uppercase text-muted-foreground">Selected</p>
                  <p class="mt-1 text-lg font-semibold">{{ audioComparison.actualCount }}</p>
                </div>
                <div class="rounded-md border bg-muted/30 p-3">
                  <p class="text-xs font-medium uppercase text-muted-foreground">Matched</p>
                  <p class="mt-1 text-lg font-semibold">{{ audioComparison.matchedFileNames.length }}</p>
                </div>
              </div>

              <div
                class="flex items-center gap-2 rounded-md border p-3 text-sm"
                :class="audioCheckPassed ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-amber-200 bg-amber-50 text-amber-800'"
              >
                <CheckCircle2 v-if="audioCheckPassed" class="size-4 shrink-0" />
                <AlertTriangle v-else class="size-4 shrink-0" />
                <span>{{ audioCheckStatus }}</span>
              </div>

              <div class="grid gap-4 lg:grid-cols-2">
                <div class="rounded-md border">
                  <div class="flex items-center justify-between gap-3 border-b p-3">
                    <p class="text-sm font-medium">Missing files</p>
                    <Button
                      variant="outline"
                      size="sm"
                      :disabled="audioComparison.missingFileNames.length === 0"
                      @click="copyAudioList(audioComparison.missingFileNames)"
                    >
                      <Copy class="size-4" />
                      Copy
                    </Button>
                  </div>
                  <div class="max-h-72 overflow-auto p-3">
                    <p v-if="audioComparison.missingFileNames.length === 0" class="text-sm text-muted-foreground">
                      No missing files.
                    </p>
                    <code
                      v-for="fileName in audioComparison.missingFileNames"
                      v-else
                      :key="fileName"
                      class="mb-1 block rounded bg-muted px-2 py-1 text-xs"
                    >
                      {{ fileName }}
                    </code>
                  </div>
                </div>

                <div class="rounded-md border">
                  <div class="flex items-center justify-between gap-3 border-b p-3">
                    <p class="text-sm font-medium">Extra files</p>
                    <Button
                      variant="outline"
                      size="sm"
                      :disabled="audioComparison.extraFileNames.length === 0"
                      @click="copyAudioList(audioComparison.extraFileNames)"
                    >
                      <Copy class="size-4" />
                      Copy
                    </Button>
                  </div>
                  <div class="max-h-72 overflow-auto p-3">
                    <p v-if="audioComparison.extraFileNames.length === 0" class="text-sm text-muted-foreground">
                      No extra files.
                    </p>
                    <code
                      v-for="fileName in audioComparison.extraFileNames"
                      v-else
                      :key="fileName"
                      class="mb-1 block rounded bg-muted px-2 py-1 text-xs"
                    >
                      {{ fileName }}
                    </code>
                  </div>
                </div>
              </div>

              <div v-if="audioFileNames.duplicateFileNames.length > 0 || audioFileNames.skippedFiles.length > 0" class="space-y-1 text-xs text-muted-foreground">
                <p v-if="audioFileNames.duplicateFileNames.length > 0">
                  Duplicate selected names: {{ audioFileNames.duplicateFileNames.join(', ') }}
                </p>
                <p v-if="audioFileNames.skippedFiles.length > 0">
                  Skipped non-audio files: {{ audioFileNames.skippedFiles.length }}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <button
            type="button"
            class="flex w-full items-center justify-between gap-3 p-5 text-left transition-colors hover:bg-muted/50"
            :aria-expanded="isCodeOutputOpen"
            @click="isCodeOutputOpen = !isCodeOutputOpen"
          >
            <span class="flex min-w-0 items-center gap-3">
              <Code2 class="size-5 shrink-0 text-primary" />
              <span class="min-w-0">
                <span class="block text-sm font-medium">Advanced: Code JSON</span>
                <span class="mt-1 block text-xs text-muted-foreground">{{ codeOutputSummary }}</span>
              </span>
            </span>
            <ChevronDown class="size-5 shrink-0 text-muted-foreground transition-transform" :class="{ 'rotate-180': isCodeOutputOpen }" />
          </button>

          <CardContent v-if="isCodeOutputOpen" class="border-t pt-5">
            <div v-if="codeLines.length === 0" class="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
              Add a quest name and drop a script file to generate code JSON.
            </div>
            <div v-else class="space-y-4">
              <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <p class="text-sm text-muted-foreground">Copies an array of line/file objects using stripped script text.</p>
                <Button variant="outline" size="sm" @click="copyCodeJson">
                  <Clipboard class="size-4" />
                  Copy JSON
                </Button>
              </div>
              <pre class="max-h-[420px] overflow-auto rounded-md bg-foreground p-4 text-xs leading-relaxed text-background"><code>{{ codeJsonOutput }}</code></pre>
            </div>
          </CardContent>
        </Card>
      </div>

      <aside class="space-y-5">
        <Card>
          <CardHeader>
            <CardTitle>Script format</CardTitle>
            <CardDescription>Input rules used by the parser.</CardDescription>
          </CardHeader>
          <CardContent class="space-y-4 text-sm text-muted-foreground">
            <section>
              <h2 class="font-medium text-foreground">Voice lines</h2>
              <p class="mt-1">Standard lines use <code class="rounded bg-muted px-1">Speaker: dialogue</code>.</p>
            </section>
            <section>
              <h2 class="font-medium text-foreground">Character changes</h2>
              <p class="mt-1">Use <code class="rounded bg-muted px-1">/$ New Speaker</code> when a line should be assigned to another character.</p>
            </section>
            <section>
              <h2 class="font-medium text-foreground">Ignored text</h2>
              <p class="mt-1">Empty lines and lines starting with <code class="rounded bg-muted px-1">//</code> or <code class="rounded bg-muted px-1">---</code> are skipped.</p>
              <p class="mt-1">Inline emotion or delivery notes should be written in curly braces, like <code class="rounded bg-muted px-1">{angry}</code>. When Strip information is enabled, those notes are removed from the generated dialogue.</p>
              <p class="mt-1">Standalone instruction lines containing <code class="rounded bg-muted px-1">Emotions will</code> are skipped because they describe the script format rather than a voice line.</p>
            </section>
            <section>
              <h2 class="font-medium text-foreground">Filenames</h2>
              <p class="mt-1">Names use <code class="rounded bg-muted px-1">quest-speaker-number</code> with non-alphanumeric characters removed.</p>
            </section>
          </CardContent>
        </Card>
      </aside>
    </div>
  </div>
</template>
