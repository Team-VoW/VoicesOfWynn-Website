<script setup lang="ts">
import { useLocalStorage, watchThrottled } from '@vueuse/core'
import {
  AudioWaveform,
  FileArchive,
  FilePlus2,
  FolderOpen,
  History,
  Keyboard,
  Maximize2,
  Play,
  Redo2,
  Repeat,
  Save,
  SaveAll,
  Square,
  Undo2,
  ZoomIn,
  ZoomOut,
} from 'lucide-vue-next'
import { computed, onMounted, provide, ref, watch } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import CleanupPanel from '../components/CleanupPanel.vue'
import CloseFilesDialog from '../components/CloseFilesDialog.vue'
import EffectsPanel from '../components/EffectsPanel.vue'
import FileListPanel from '../components/FileListPanel.vue'
import MarkerExportDialog from '../components/MarkerExportDialog.vue'
import MarkerPropertiesDialog from '../components/MarkerPropertiesDialog.vue'
import MarkersPanel from '../components/MarkersPanel.vue'
import { MARKER_ACTIONS } from '../composables/markerActions'
import MatchLoudnessPanel from '../components/MatchLoudnessPanel.vue'
import ScrubNumber from '../components/ScrubNumber.vue'
import SilencePanel from '../components/SilencePanel.vue'
import SpectralToolbar from '../components/SpectralToolbar.vue'
import StatusBar from '../components/StatusBar.vue'
import WaveformView from '../components/WaveformView.vue'
import { useEditorPlayback } from '../composables/useEditorPlayback'
import { usePreviewDisplay } from '../composables/usePreviewDisplay'
import { SHORTCUT_HELP, useEditorShortcuts } from '../composables/useEditorShortcuts'
import {
  canOpenFolders,
  canWriteInPlace,
  ensureWritable,
  fromDataTransfer,
  fromFileList,
  pickDirectory,
  pickFiles,
  readDirectory,
  type OpenedFile,
} from '../lib/fileAccess'
import { stepName } from '../lib/cleanupSteps'
import type { Marker } from '../lib/markers'
import { deleteRange } from '../lib/operations'
import type { SpectralSelection } from '../lib/spectral'
import { recallFolder, rememberFolder } from '../lib/recentFolder'
import {
  isDirty,
  useAudioWorkspace,
  type EditorDocument,
  type SpectralTool,
} from '../stores/workspace'

const workspace = useAudioWorkspace()
const playback = useEditorPlayback()
const waveform = ref<InstanceType<typeof WaveformView> | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
const folderInput = ref<HTMLInputElement | null>(null)
const views = useLocalStorage(
  'vow.audioEditor.views',
  { waveform: true, spectral: false },
  { mergeDefaults: true },
)
const VIEW_OPTIONS = [
  { id: 'waveform', label: 'Waveform' },
  { id: 'spectral', label: 'Spectral' },
] as const

/** Toggles a display, keeping at least one of the two on. */
function toggleView(id: 'waveform' | 'spectral') {
  const other = id === 'waveform' ? 'spectral' : 'waveform'
  const next = { ...views.value, [id]: !views.value[id] }
  if (!next.waveform && !next.spectral) next[other] = true
  views.value = next
}
const tab = useLocalStorage<'effects' | 'cleanup' | 'loudness' | 'silence'>(
  'vow.audioEditor.tab',
  'effects',
)
// Spectral editing moved onto the spectrogram itself; that tab is gone.
if (!['effects', 'cleanup', 'loudness', 'silence'].includes(tab.value)) tab.value = 'effects'
const showShortcuts = ref(false)
const dragDepth = ref(0)
const recentFolder = ref<FileSystemDirectoryHandle | null>(null)
const writesInPlace = canWriteInPlace()

const doc = computed(() => workspace.activeDocument)
const lastUndo = computed(() => doc.value?.undo.at(-1)?.label)
const lastRedo = computed(() => doc.value?.redo.at(-1)?.label)
const previewDisplay = usePreviewDisplay(doc)
const showMarkerExport = ref(false)
const markersPanel = ref<InstanceType<typeof MarkersPanel> | null>(null)
/** The marker whose properties dialog is open. */
const editingMarkerId = ref<number | null>(null)
const busyLabel = computed(
  () => workspace.busy ?? (workspace.previewRendering ? 'Rendering preview…' : null),
)
const previewLabel = computed(() => {
  const step = workspace.preview
  if (!step) return null
  const name = stepName(step)
  return workspace.previewBypass ? `Original · B for ${name}` : `${name} preview · B for original`
})
/** While the Silence tab is open, shade what Trim silence would cut. */
const cutPreview = computed(() => {
  const current = doc.value
  if (!current || tab.value !== 'silence' || !workspace.trimSettings.showPreview) return null
  const plan = workspace.trimPlanFor(current)
  if (!plan || (plan.removedLeadSeconds === 0 && plan.removedTailSeconds === 0)) return null
  return plan.keep
})

function reportError(error: unknown, fallback: string) {
  toast.error(error instanceof Error ? error.message : fallback)
}

async function open(files: OpenedFile[]) {
  if (files.length === 0) {
    toast.warning('No audio files found there.')
    return
  }
  const added = await workspace.addFiles(files)
  if (added < files.length) toast.info(`${files.length - added} file(s) were already open.`)
}

async function openFolder() {
  if (!canOpenFolders()) {
    folderInput.value?.click()
    return
  }
  try {
    const picked = await pickDirectory()
    if (!picked) return
    await open(picked.files)
    await rememberFolder(picked.directory)
    recentFolder.value = picked.directory
  } catch (error) {
    reportError(error, 'The folder could not be opened.')
  }
}

async function openFiles() {
  if (!writesInPlace) {
    fileInput.value?.click()
    return
  }
  try {
    const files = await pickFiles()
    if (files) await open(files)
  } catch (error) {
    reportError(error, 'The files could not be opened.')
  }
}

async function reopenFolder() {
  const directory = recentFolder.value
  if (!directory) return
  try {
    if (!(await ensureWritable(directory))) return
    await open(await readDirectory(directory))
  } catch (error) {
    reportError(error, 'The folder could not be reopened.')
  }
}

function onInputChange(event: Event) {
  const input = event.target as HTMLInputElement
  void open(fromFileList(Array.from(input.files ?? [])))
  input.value = ''
}

async function onDrop(event: DragEvent) {
  dragDepth.value = 0
  if (!event.dataTransfer) return
  try {
    await open(await fromDataTransfer(event.dataTransfer))
  } catch (error) {
    reportError(error, 'The dropped files could not be opened.')
  }
}

async function save(target: EditorDocument | null, saveMode: 'save' | 'saveAs' = 'save') {
  if (!target || target.status !== 'ready') return
  try {
    const saved = await workspace.save(target, saveMode)
    if (saved) toast.success(`Saved ${target.name}`)
  } catch (error) {
    reportError(error, `${target.name} could not be saved.`)
  }
}

async function saveAll() {
  if (workspace.dirtyDocuments.length === 0) {
    toast.info('Nothing to save.')
    return
  }
  try {
    const { written, zipped } = await workspace.saveAll()
    toast.success(
      `Saved ${written} file(s) over the originals${zipped ? `; ${zipped} without a writable original went into a ZIP download` : ''}.`,
    )
  } catch (error) {
    reportError(error, 'Saving failed.')
  }
}

async function exportZip() {
  try {
    await workspace.exportZip(workspace.batchDocuments.filter((entry) => entry.status !== 'error'))
  } catch (error) {
    reportError(error, 'The ZIP could not be built.')
  }
}

/** Files waiting on the save-or-discard dialog. */
const closing = ref<EditorDocument[] | null>(null)

function close(targets: EditorDocument[]) {
  if (targets.some(isDirty)) closing.value = targets
  else workspace.closeMany(targets)
}

async function togglePlay(loop = false) {
  const current = doc.value
  if (!current) return
  if (playback.playing.value) {
    playback.stop()
    return
  }
  try {
    const channels = await workspace.ensureChannels(current)
    await playback.play(current, workspace.playbackChannels(current, channels), loop)
  } catch (error) {
    reportError(error, 'Playback failed.')
  }
}

/** Runs a spectral edit from the keyboard or the spot healing brush. */
function spectralEdit(mode: 'delete' | 'heal', selection?: SpectralSelection) {
  const current = doc.value
  if (!current || current.status !== 'ready') return
  playback.stop()
  void workspace
    .applySpectral(current, mode, selection)
    .catch((error) => reportError(error, mode === 'heal' ? 'Heal failed.' : 'Delete failed.'))
}

function deleteSelection() {
  const current = doc.value
  const selection = current?.selection
  if (!current || !selection) return
  // With areas picked on the spectrogram, Delete silences those rather than cutting time.
  if (current.spectralSelection) {
    spectralEdit('delete')
    return
  }
  playback.stop()
  void workspace
    .apply(current, 'Delete', (channels) => ({
      channels: deleteRange(channels, selection),
      selection: null,
      cursor: selection.start,
      splices: [{ start: selection.start, removed: selection.end - selection.start, inserted: 0 }],
    }))
    .catch((error) => reportError(error, 'Delete failed.'))
}

function onSelect(selection: { start: number; end: number } | null, cursor: number) {
  if (!doc.value) return
  if (selection !== doc.value.selection) workspace.setSpectralSelection(doc.value, null)
  doc.value.selection = selection
  doc.value.cursor = cursor
}

/** Picking a spectral tool brings the spectral display up if it is hidden. */
function setSpectralTool(tool: SpectralTool) {
  workspace.spectralSettings.tool = tool
  if (tool !== 'time' && !views.value.spectral) views.value = { ...views.value, spectral: true }
}

function addMarker() {
  const current = doc.value
  if (current?.status === 'ready') workspace.addMarker(current)
}

function revealMarker(marker: Marker) {
  waveform.value?.reveal(marker.start, marker.start + marker.length)
}

provide(MARKER_ACTIONS, {
  play: (marker) => {
    const current = doc.value
    if (!current) return
    workspace.selectMarker(current, marker.id)
    playback.stop()
    void togglePlay(false)
  },
  reveal: (marker, zoom) => {
    const end = marker.start + Math.max(marker.length, 1)
    if (zoom) waveform.value?.zoomTo(marker.start, end)
    else waveform.value?.reveal(marker.start, end)
  },
  rename: (marker) => void markersPanel.value?.startRename(marker),
  edit: (marker) => (editingMarkerId.value = marker.id),
})

function onMarkerSelect(id: number) {
  if (doc.value) workspace.selectMarker(doc.value, id)
}

/** Clicks and drags on the waveform go through the workspace so they can snap to zero crossings. */
function onWaveformSelect(selection: { start: number; end: number } | null, cursor: number) {
  if (doc.value) workspace.select(doc.value, selection, cursor)
}

/** Clipboard edits from the keyboard; like Delete, the ones that change audio stop playback. */
async function clipboardEdit(
  task: (target: EditorDocument) => Promise<void>,
  fallback: string,
  stopsPlayback = true,
) {
  const current = doc.value
  if (!current || current.status !== 'ready') return
  if (stopsPlayback) playback.stop()
  try {
    await task(current)
  } catch (error) {
    reportError(error, fallback)
  }
}

useEditorShortcuts({
  togglePlay: () => void togglePlay(false),
  toggleLoop: () => void togglePlay(true),
  selectAll: () => doc.value && onSelect({ start: 0, end: doc.value.frames }, 0),
  deleteSelection,
  cut: () => void clipboardEdit(workspace.cut, 'Cut failed.'),
  copy: () => void clipboardEdit(workspace.copy, 'Copy failed.', false),
  paste: () => void clipboardEdit(workspace.paste, 'Paste failed.'),
  mixPaste: () => void clipboardEdit(workspace.mixPaste, 'Mix paste failed.'),
  copyToNew: () => void clipboardEdit(workspace.copyToNew, 'Copy to new failed.', false),
  zeroCrossings: (adjustment) =>
    doc.value && workspace.adjustSelectionToZeroCrossings(doc.value, adjustment),
  undo: () => {
    if (!doc.value) return
    playback.stop()
    workspace.undo(doc.value)
  },
  redo: () => {
    if (!doc.value) return
    playback.stop()
    workspace.redo(doc.value)
  },
  save: () => void save(doc.value),
  saveAs: () => void save(doc.value, 'saveAs'),
  previousFile: () => workspace.step(-1),
  nextFile: () => workspace.step(1),
  zoomIn: () => waveform.value?.zoomIn(),
  zoomOut: () => waveform.value?.zoomOut(),
  zoomToFit: () => waveform.value?.zoomToFit(),
  toStart: () => doc.value && onSelect(doc.value.selection, 0),
  toEnd: () => doc.value && onSelect(doc.value.selection, doc.value.frames),
  clearSelection: () => doc.value && onSelect(null, doc.value.cursor),
  toggleBypass: () => {
    if (workspace.preview) workspace.previewBypass = !workspace.previewBypass
  },
  addMarker,
  spectralTool: setSpectralTool,
  currentSpectralTool: () => workspace.spectralSettings.tool,
  heal: () => doc.value?.spectralSelection && spectralEdit('heal'),
})

// While playing, follow what should be heard: a preview's new settings, the A/B switch, or an
// edit applied mid-playback. Throttled so dragging a value does not re-render every frame.
watchThrottled(
  () =>
    [
      workspace.preview,
      workspace.previewBypass,
      workspace.previewRender,
      doc.value?.channels,
      doc.value?.selection?.start,
      doc.value?.selection?.end,
    ] as const,
  () => {
    const current = doc.value
    if (!playback.playing.value || !current?.channels) return
    playback.swap(workspace.playbackChannels(current, current.channels))
  },
  { throttle: 60, trailing: true },
)

// Switching files stops playback; the open file needs its samples to edit and draw.
watch(
  () => workspace.activeId,
  () => playback.stop(),
)
watch(
  () => [doc.value?.id, doc.value?.state] as const,
  () => {
    const current = doc.value
    if (!current || current.status === 'error') return
    void workspace.ensureChannels(current).catch(() => {})
  },
  { immediate: true },
)

onMounted(async () => {
  if (canOpenFolders()) recentFolder.value = await recallFolder()
})
</script>

<template>
  <div
    class="relative flex min-h-[600px] flex-col gap-3 md:h-[calc(100dvh-3rem)]"
    @dragenter.prevent="dragDepth++"
    @dragover.prevent
    @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
    @drop.prevent="onDrop"
  >
    <header class="flex flex-wrap items-center gap-2">
      <h1 class="mr-2 text-xl font-semibold tracking-tight">Audio editor</h1>

      <div class="flex flex-wrap items-center gap-1">
        <Button size="sm" variant="outline" @click="openFolder"><FolderOpen /> Open folder</Button>
        <Button size="sm" variant="outline" @click="openFiles"><FilePlus2 /> Open files</Button>
        <Button
          v-if="recentFolder"
          size="sm"
          variant="ghost"
          :title="`Reopen ${recentFolder.name}`"
          @click="reopenFolder"
        >
          <History /> {{ recentFolder.name }}
        </Button>
      </div>

      <div class="mx-1 h-6 w-px bg-border" />

      <div class="flex items-center gap-1">
        <Button
          size="icon-sm"
          :variant="playback.playing.value ? 'default' : 'outline'"
          :disabled="!doc || doc.status !== 'ready'"
          :aria-label="playback.playing.value ? 'Stop (Space)' : 'Play (Space)'"
          :title="playback.playing.value ? 'Stop (Space)' : 'Play (Space)'"
          @click="togglePlay(false)"
        >
          <Square v-if="playback.playing.value" /><Play v-else />
        </Button>
        <Button
          size="icon-sm"
          :variant="playback.playing.value && playback.looping.value ? 'default' : 'outline'"
          :disabled="!doc || doc.status !== 'ready'"
          aria-label="Loop selection (Shift+Space)"
          title="Loop selection (Shift+Space)"
          @click="togglePlay(true)"
        >
          <Repeat />
        </Button>
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!lastUndo"
          :aria-label="lastUndo ? `Undo ${lastUndo}` : 'Undo'"
          :title="lastUndo ? `Undo ${lastUndo} (Ctrl+Z)` : 'Undo (Ctrl+Z)'"
          @click="doc && workspace.undo(doc)"
        >
          <Undo2 />
        </Button>
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!lastRedo"
          :aria-label="lastRedo ? `Redo ${lastRedo}` : 'Redo'"
          :title="lastRedo ? `Redo ${lastRedo} (Ctrl+Shift+Z)` : 'Redo (Ctrl+Shift+Z)'"
          @click="doc && workspace.redo(doc)"
        >
          <Redo2 />
        </Button>
      </div>

      <div class="mx-1 h-6 w-px bg-border" />

      <div class="flex items-center gap-1">
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!doc"
          aria-label="Zoom in (+)"
          title="Zoom in (+)"
          @click="waveform?.zoomIn()"
          ><ZoomIn
        /></Button>
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!doc"
          aria-label="Zoom out (−)"
          title="Zoom out (−)"
          @click="waveform?.zoomOut()"
          ><ZoomOut
        /></Button>
        <Button
          size="icon-sm"
          variant="outline"
          :disabled="!doc"
          aria-label="Zoom to fit (0)"
          title="Zoom to fit (0)"
          @click="waveform?.zoomToFit()"
          ><Maximize2
        /></Button>
        <div
          class="ml-1 inline-flex rounded-md border bg-background p-0.5"
          role="group"
          aria-label="Displays"
        >
          <button
            v-for="option in VIEW_OPTIONS"
            :key="option.id"
            type="button"
            :aria-pressed="views[option.id]"
            class="rounded px-2 py-1 text-xs font-medium"
            :class="
              views[option.id]
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:text-foreground'
            "
            :title="
              views[option.id]
                ? `Hide the ${option.label.toLowerCase()} display`
                : `Show the ${option.label.toLowerCase()} display`
            "
            @click="toggleView(option.id)"
          >
            {{ option.label }}
          </button>
        </div>
      </div>

      <div v-if="views.spectral" class="flex items-center gap-2">
        <SpectralToolbar
          :model-value="workspace.spectralSettings.tool"
          @update:model-value="setSpectralTool"
        />
        <label
          v-if="['brush', 'heal'].includes(workspace.spectralSettings.tool)"
          class="flex items-center gap-1.5 text-xs text-muted-foreground"
        >
          Size
          <ScrubNumber
            v-model="workspace.spectralSettings.brushSize"
            label="Brush size"
            :min="4"
            :max="200"
            unit="px"
            title="Diameter of the brush and the spot healing brush"
          />
        </label>
      </div>

      <div class="ml-auto flex items-center gap-1">
        <Button
          size="sm"
          variant="outline"
          :disabled="!doc || doc.status !== 'ready'"
          :title="writesInPlace ? 'Save over the original (Ctrl+S)' : 'Download (Ctrl+S)'"
          @click="save(doc)"
        >
          <Save /> Save
        </Button>
        <Button
          size="sm"
          variant="outline"
          :disabled="!workspace.dirtyDocuments.length"
          @click="saveAll"
        >
          <SaveAll /> Save all
        </Button>
        <Button
          size="sm"
          variant="outline"
          :disabled="!workspace.documents.length"
          title="Download the ticked files (or all) as one ZIP of WAVs"
          @click="exportZip"
        >
          <FileArchive /> ZIP
        </Button>
        <Button
          size="icon-sm"
          :variant="showShortcuts ? 'default' : 'ghost'"
          aria-label="Keyboard shortcuts"
          title="Keyboard shortcuts"
          @click="showShortcuts = !showShortcuts"
        >
          <Keyboard />
        </Button>
      </div>
    </header>

    <p
      v-if="!writesInPlace"
      class="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900"
    >
      This browser cannot write to files on disk, so saving downloads a copy instead. Use Chrome or
      Edge to save straight over the originals.
    </p>

    <dl
      v-if="showShortcuts"
      class="grid grid-cols-2 gap-x-6 gap-y-1 rounded-md border bg-card px-4 py-3 text-xs sm:grid-cols-3 lg:grid-cols-4"
    >
      <div v-for="[keys, action] in SHORTCUT_HELP" :key="keys" class="flex justify-between gap-3">
        <dt class="font-mono text-foreground">{{ keys }}</dt>
        <dd class="text-muted-foreground">{{ action }}</dd>
      </div>
    </dl>

    <div
      v-if="workspace.documents.length === 0"
      class="grid flex-1 place-items-center rounded-md border-2 border-dashed p-8 text-center"
      :class="dragDepth > 0 ? 'border-primary bg-primary/5' : 'border-border bg-muted/40'"
    >
      <div class="max-w-md space-y-3">
        <AudioWaveform class="mx-auto size-10 text-primary" />
        <p class="font-medium">Open a folder of voice lines, or drop audio files here</p>
        <p class="text-sm text-muted-foreground">
          Everything stays on your computer. Edit a line, press Ctrl+S to save over the original,
          then ↓ for the next one. Match Loudness levels a whole batch to −23 / −18 / −13 LUFS.
        </p>
        <div class="flex justify-center gap-2">
          <Button @click="openFolder"><FolderOpen /> Open folder</Button>
          <Button variant="outline" @click="openFiles"><FilePlus2 /> Open files</Button>
        </div>
      </div>
    </div>

    <div
      v-else
      class="grid min-h-0 flex-1 gap-3 lg:grid-cols-[minmax(220px,280px)_minmax(0,1fr)_320px]"
    >
      <div class="flex min-h-0 flex-col gap-3">
        <FileListPanel class="max-h-72 min-h-0 flex-1 lg:max-h-none" @close="close" />
        <MarkersPanel
          ref="markersPanel"
          :doc="doc"
          class="max-h-72 lg:max-h-none"
          @reveal="revealMarker"
          @export="showMarkerExport = true"
        />
      </div>

      <section class="flex min-h-[320px] min-w-0 flex-col gap-2" aria-label="Waveform editor">
        <template v-if="doc">
          <WaveformView
            ref="waveform"
            :doc="doc"
            :playhead="playback.position.value"
            :views="views"
            :cut-preview="cutPreview"
            :preview="previewDisplay"
            :preview-label="previewLabel"
            :busy="busyLabel"
            @select="onWaveformSelect"
            @view="(view) => doc && (doc.view = view)"
            @marker-select="onMarkerSelect"
            @markers-commit="
              (label, markers, before) => doc && workspace.setMarkers(doc, label, markers, before)
            "
            @spectral-select="(selection) => doc && workspace.setSpectralSelection(doc, selection)"
            @spot-heal="(selection) => spectralEdit('heal', selection)"
          />
          <StatusBar :doc="doc" :playhead="playback.position.value" />
        </template>
        <div
          v-else
          class="grid flex-1 place-items-center rounded-md border text-sm text-muted-foreground"
        >
          Pick a file on the left.
        </div>
      </section>

      <aside class="flex min-h-0 min-w-0 flex-col rounded-md border bg-card">
        <div class="flex border-b" role="tablist" aria-label="Tools">
          <button
            v-for="option in [
              { id: 'cleanup', label: 'Cleanup' },
              { id: 'effects', label: 'Effects' },
              { id: 'silence', label: 'Silence' },
              { id: 'loudness', label: 'Loudness' },
            ] as const"
            :key="option.id"
            type="button"
            role="tab"
            :aria-selected="tab === option.id"
            class="flex-1 border-b-2 px-2 py-2 text-sm font-medium"
            :class="
              tab === option.id
                ? 'border-primary text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            "
            @click="tab = option.id"
          >
            {{ option.label }}
          </button>
        </div>
        <div
          class="relative flex min-h-0 flex-1 flex-col overflow-y-auto overflow-x-hidden p-3"
          role="tabpanel"
        >
          <template v-if="tab === 'effects' || tab === 'cleanup'">
            <template v-if="doc && doc.status === 'ready'">
              <EffectsPanel v-if="tab === 'effects'" :doc="doc" />
              <CleanupPanel v-else :doc="doc" />
            </template>
            <p v-else class="text-sm text-muted-foreground">Open a file to edit it.</p>
          </template>
          <MatchLoudnessPanel v-else-if="tab === 'loudness'" />
          <SilencePanel v-else />
        </div>
      </aside>
    </div>

    <div
      v-if="dragDepth > 0 && workspace.documents.length > 0"
      class="pointer-events-none absolute inset-0 grid place-items-center rounded-md border-2 border-dashed border-primary bg-primary/10 text-sm font-medium"
    >
      Drop to open
    </div>

    <CloseFilesDialog v-model="closing" />
    <MarkerExportDialog v-model="showMarkerExport" :doc="doc" />
    <MarkerPropertiesDialog v-model="editingMarkerId" :doc="doc" />

    <input
      ref="fileInput"
      type="file"
      multiple
      accept="audio/*,.wav,.ogg,.mp3,.flac"
      class="hidden"
      @change="onInputChange"
    />
    <input ref="folderInput" type="file" webkitdirectory class="hidden" @change="onInputChange" />
  </div>
</template>
