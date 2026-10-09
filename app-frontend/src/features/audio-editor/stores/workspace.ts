import { useEventListener, useLocalStorage, watchDebounced } from '@vueuse/core'
import { computed, effectScope, markRaw, reactive, ref, shallowRef, toRaw } from 'vue'
import { runAnalysisJob, WORKER_COUNT } from '../lib/analysisClient'
import { decodeAudioFile } from '../lib/decode'
import {
  canWriteInPlace,
  download,
  saveAs,
  withWavExtension,
  writeFile,
  type OpenedFile,
} from '../lib/fileAccess'
import {
  adjustToZeroCrossings,
  conformClip,
  mixPaste as mixPasteClip,
  pasteInsert,
  zeroCrossing,
  type ClipboardAudio,
  type ZeroCrossingAdjustment,
} from '../lib/editing'
import {
  describeStep,
  offsetStep,
  stepProblem,
  stepRange,
  stepReach,
  type CleanupKind,
  type CleanupStep,
  type ProcessStep,
  type SpectralStep,
} from '../lib/cleanupSteps'
import type { FrequencyScale } from '../lib/frequencyScale'
import type { AudioAnalysis } from '../lib/loudness'
import {
  HEAVY_LIMITING_DB,
  SHORTFALL_LU,
  type MatchOptions,
  type PeakControl,
} from '../lib/matchLoudness'
import {
  clampMarkers,
  cropSplices,
  nextMarkerName,
  readMarkers,
  sortMarkers,
  spliceMarkers,
  withMarkers,
  type Marker,
  type MarkerData,
  type Splice,
} from '../lib/markers'
import type { NoisePrint } from '../lib/noiseReduction'
import { planTrim, type TrimPlan } from '../lib/silence'
import {
  selectionBounds,
  type HealDirection,
  type SpectralMode,
  type SpectralSelection,
} from '../lib/spectral'
import type { SpectrumColors } from '../lib/spectrumColors'
import { crop, deleteRange, frameCount, type Channels, type FrameRange } from '../lib/operations'
import { buildPeaks, type PeakPyramid } from '../lib/peaks'
import { applyPatch, diffPatch, patchBytes, type AudioPatch } from '../lib/undoPatch'
import { defaultLayout, encodeWav, type WavLayout } from '../lib/wav'
import { createZip } from '../lib/zip'
import { SILENCE_THRESHOLD_DB } from '@/features/tools/lib/audioChecks'

interface Snapshot {
  label: string
  /**
   * How to get this step's audio back from the audio after it: only the frames the edit changed.
   * Null for steps that left the audio alone (marker edits).
   */
  audio: AudioPatch | null
  markers: Marker[]
  state: number
  selection: FrameRange | null
  spectralSelection: SpectralSelection | null
  cursor: number
}

/** What a drag on the spectral display does; the waveform always selects time. */
export type SpectralTool = 'time' | 'marquee' | 'lasso' | 'brush' | 'frequency' | 'heal'

export interface EditorDocument {
  id: number
  name: string
  path: string
  /** What is on disk now: the opened file, replaced by the written bytes after each save. */
  file: File
  handle: FileSystemFileHandle | null
  savesInPlace: boolean
  status: 'loading' | 'ready' | 'error'
  error: string | null
  sampleRate: number
  layout: WavLayout | null
  channelCount: number
  frames: number
  /** Null while evicted; re-decoded from `file` on demand. */
  channels: Channels | null
  peaks: PeakPyramid | null
  analysis: AudioAnalysis | null
  /** The `state` `analysis` was measured on; it lags behind while a new measurement runs. */
  analysisState: number
  analyzing: boolean
  undo: Snapshot[]
  redo: Snapshot[]
  /** Identifies the current audio; `savedState` is the one on disk, so undoing to it is clean. */
  state: number
  savedState: number
  selection: FrameRange | null
  /**
   * Areas of time × frequency picked on the spectral display. While there is one, `selection` is
   * the time it spans, so playing and looping play just that stretch.
   */
  spectralSelection: SpectralSelection | null
  cursor: number
  view: { start: number; samplesPerPixel: number } | null
  checked: boolean
  /** Silence threshold set on this file; null follows its suggested one. */
  trimThresholdDb: number | null
  /** In time order. Read from the file the first time it is decoded. */
  markers: Marker[]
  markersLoaded: boolean
  /** The marker picked in the list or on the ruler. */
  selectedMarkerId: number | null
}

export interface EditResult {
  channels: Channels
  selection?: FrameRange | null
  cursor?: number
  /**
   * How the edit moved audio around, applied in order to the markers. Without it, an edit that
   * keeps the length leaves them be and one that changes it only drops what falls off the end.
   */
  splices?: Splice[]
}

export interface MarkerExport {
  markers: Marker[]
  names: string[]
  /** `folder` writes into `directory`; `open` adds them to the editor as unsaved files. */
  target: 'zip' | 'folder' | 'open'
  directory?: FileSystemDirectoryHandle
  zipName?: string
}

/** How far Edit > Zero Crossings looks, and how far snapping may move a dragged edge. */
const ZERO_CROSSING_REACH_SECONDS = 0.1
const SNAP_REACH_SECONDS = 0.02
const UNDO_LIMIT = 50
const UNDO_BUDGET_BYTES = 512 * 1024 * 1024
/** Edits on files longer than this let the busy indicator paint before they block the page. */
const HEAVY_SAMPLES = 2_000_000
/** Audio either side of a selection a previewed step also processes, so filters settle first. */
const STEP_PAD_SECONDS = 1
// Two in flight per worker, so each has the next file read from disk by the time it finishes.
const LOAD_CONCURRENCY = WORKER_COUNT * 2

/**
 * `name`, or `name (2).wav` and up when it is taken: `line.mp3` and `line.wav` both save as
 * `line.wav`, and two same-named files can be open. Case-insensitive, as most file systems are.
 */
function uniqueName(name: string, taken: Set<string>) {
  const stem = name.replace(/\.wav$/, '')
  let candidate = name
  for (let n = 2; taken.has(candidate.toLowerCase()); n++) candidate = `${stem} (${n}).wav`
  taken.add(candidate.toLowerCase())
  return candidate
}

export function isDirty(doc: EditorDocument) {
  return doc.state !== doc.savedState
}

/** Resolves once the browser has painted, so a busy indicator shows before blocking work. */
function nextPaint() {
  return new Promise<void>((resolve) => requestAnimationFrame(() => setTimeout(resolve)))
}

function sameRange(a: FrameRange | null | undefined, b: FrameRange | null | undefined) {
  return a?.start === b?.start && a?.end === b?.end
}

interface PreviewRender {
  docId: number
  /** The audio the step ran over. */
  channels: Channels
  step: ProcessStep
  selection: FrameRange | null
  output: Channels
  clicks: number
}

function createWorkspace() {
  const documents = ref<EditorDocument[]>([])
  const activeId = ref<number | null>(null)
  const search = ref('')
  const targetLufs = useLocalStorage('vow.audioEditor.targetLufs', -18)
  const ceilingDbtp = useLocalStorage('vow.audioEditor.ceilingDbtp', -1)
  const peakControl = useLocalStorage<PeakControl>('vow.audioEditor.peakControl', 'auto')
  const trimSettings = useLocalStorage(
    'vow.audioEditor.trim',
    { keepLeadMs: 50, keepTailMs: 300, showPreview: true },
    { mergeDefaults: true },
  )
  const mixPasteSettings = useLocalStorage(
    'vow.audioEditor.mixPaste',
    { mode: 'overlap' as 'overlap' | 'overwrite', volumeDb: 0, crossfadeMs: 10 },
    { mergeDefaults: true },
  )
  const snapToZeroCrossings = useLocalStorage('vow.audioEditor.snapToZeroCrossings', false)
  /** Shared by every open file, like Audition's clipboard. */
  const clipboard = ref<ClipboardAudio | null>(null)
  /** The last captured noise print, used by Noise reduction on any file at the same rate. */
  const noisePrint = ref<NoisePrint | null>(null)
  /** The cleanup step being auditioned: playback runs it over the audio without changing it. */
  const preview = ref<ProcessStep | null>(null)
  /** A/B: play the untouched audio while a preview is set. */
  const previewBypass = ref(false)
  const spectralSettings = useLocalStorage(
    'vow.audioEditor.spectral',
    {
      tool: 'time' as SpectralTool,
      /** Diameter of the brush and the spot healing brush, in screen pixels. */
      brushSize: 24,
      /** Percent of the widest feather. */
      feather: 35,
      gainDb: -12,
      healDirection: 'auto' as HealDirection,
      fftSize: 2048,
      rangeDb: 84,
      scale: 'log' as FrequencyScale,
      colors: 'ocean' as SpectrumColors,
    },
    { mergeDefaults: true },
  )
  /** The frequency range the spectral display is zoomed to, or null for all of it. */
  const frequencyView = ref<{ low: number; high: number } | null>(null)
  /** The previewed step rendered by a worker; playback uses it once it matches the audio. */
  const previewRender = shallowRef<PreviewRender | null>(null)
  const previewRendering = ref(false)
  /** What the editor is busy with, for the indicator over the waveform. */
  const busy = ref<string | null>(null)
  let nextId = 1
  let nextMarkerId = 1
  let nextState = 1
  let nextUntitled = 1

  const activeDocument = computed(
    () => documents.value.find((doc) => doc.id === activeId.value) ?? null,
  )
  const visibleDocuments = computed(() => {
    const query = search.value.trim().toLowerCase()
    return query
      ? documents.value.filter((doc) => doc.path.toLowerCase().includes(query))
      : documents.value
  })
  const dirtyDocuments = computed(() => documents.value.filter(isDirty))
  const checkedDocuments = computed(() => documents.value.filter((doc) => doc.checked))
  /** Batch actions run on the ticked files, or on every file when none are ticked. */
  const batchDocuments = computed(() =>
    checkedDocuments.value.length > 0 ? checkedDocuments.value : documents.value,
  )

  // Loading: a bounded queue so dropping 500 files does not read 500 at once.
  const loadQueue: EditorDocument[] = []
  const inFlight = new Map<number, Promise<void>>()
  let loading = 0

  function pumpLoads() {
    while (loading < LOAD_CONCURRENCY && loadQueue.length > 0) {
      const doc = loadQueue.shift()!
      loading++
      const load = initialLoad(doc).finally(() => {
        inFlight.delete(doc.id)
        loading--
        pumpLoads()
      })
      inFlight.set(doc.id, load)
    }
  }

  /** Waits for the document's first load, starting it now if it is still queued. */
  async function awaitLoad(doc: EditorDocument) {
    const queued = loadQueue.indexOf(doc)
    if (queued >= 0) {
      loadQueue.splice(queued, 1)
      await initialLoad(doc)
    } else {
      await inFlight.get(doc.id)
    }
  }

  /**
   * WAVs are parsed, summarised and measured entirely in a worker, which hands back only the peaks
   * and the analysis; the main thread decodes samples just for the file being looked at. Anything
   * the worker cannot parse falls back to the browser's decoder here.
   */
  async function initialLoad(doc: EditorDocument) {
    if (!doc.channels && doc.file.name.toLowerCase().endsWith('.wav')) {
      const state = doc.state
      try {
        const bytes = await doc.file.arrayBuffer()
        const loaded = await runAnalysisJob({ kind: 'load', bytes }, [bytes])
        // The active file may have been decoded here meanwhile; it already has all of this.
        if (doc.status !== 'ready') {
          doc.sampleRate = loaded.sampleRate
          doc.layout = markRaw(loaded.layout)
          doc.savesInPlace = true
          doc.channelCount = loaded.channelCount
          doc.frames = loaded.frames
          doc.peaks = markRaw(loaded.peaks)
          adoptMarkers(doc)
          doc.status = 'ready'
        }
        if (doc.state === state) setAnalysis(doc, loaded.analysis)
        return
      } catch {
        // Not a WAV this parser reads (ADPCM, …): let the browser try below.
      }
    }
    try {
      await ensureChannels(doc)
      if (!doc.analysis) await refreshAnalysis(doc)
    } catch {
      // ensureChannels already recorded the error on the document.
    } finally {
      evictFarDocuments()
    }
  }

  /**
   * Opens the files that are not open already. Two files can share a path (same-named files picked
   * from different folders); their handles tell whether they are the same file on disk.
   */
  async function addFiles(opened: OpenedFile[]) {
    const byPath = new Map<string, (FileSystemFileHandle | null)[]>()
    for (const doc of documents.value)
      byPath.set(doc.path, [...(byPath.get(doc.path) ?? []), doc.handle])
    const fresh: OpenedFile[] = []
    for (const entry of opened) {
      const handles = byPath.get(entry.path) ?? []
      let open = false
      for (const handle of handles) {
        open = !handle || !entry.handle || (await handle.isSameEntry(entry.handle))
        if (open) break
      }
      if (open) continue
      fresh.push(entry)
      byPath.set(entry.path, [...handles, entry.handle])
    }
    return addDocuments(fresh)
  }

  function addDocuments(opened: OpenedFile[]) {
    const added: EditorDocument[] = []
    for (const entry of opened) {
      const state = nextState++
      documents.value.push({
        id: nextId++,
        name: entry.file.name,
        path: entry.path,
        file: entry.file,
        handle: entry.handle,
        savesInPlace: false,
        status: 'loading',
        error: null,
        sampleRate: 0,
        layout: null,
        channelCount: 0,
        frames: 0,
        channels: null,
        peaks: null,
        analysis: null,
        analysisState: 0,
        analyzing: false,
        undo: [],
        redo: [],
        state,
        savedState: state,
        selection: null,
        spectralSelection: null,
        cursor: 0,
        view: null,
        checked: false,
        trimThresholdDb: null,
        markers: [],
        markersLoaded: false,
        selectedMarkerId: null,
      })
      // Read back through the reactive array so later writes are tracked.
      added.push(documents.value[documents.value.length - 1]!)
    }
    if (activeId.value === null && added[0]) activeId.value = added[0].id
    loadQueue.push(...added)
    pumpLoads()
    return added.length
  }

  /** The markers stored in the file, read once: after that the document's own are the truth. */
  function adoptMarkers(doc: EditorDocument) {
    if (doc.markersLoaded || !doc.layout) return
    doc.markersLoaded = true
    doc.markers = readMarkers(doc.layout, doc.sampleRate).map((marker) => ({
      ...marker,
      id: nextMarkerId++,
    }))
  }

  const decoding = new Map<number, Promise<Channels>>()

  /** The document's samples, decoded on first use; one decode at a time per document. */
  function ensureChannels(doc: EditorDocument): Promise<Channels> {
    if (doc.channels) return Promise.resolve(doc.channels)
    let pending = decoding.get(doc.id)
    if (!pending) {
      pending = decode(doc).finally(() => decoding.delete(doc.id))
      decoding.set(doc.id, pending)
    }
    return pending
  }

  /** WAVs are parsed off the page (a 2-hour take would block it for a second); others here. */
  async function decodeOffPage(doc: EditorDocument) {
    if (doc.file.name.toLowerCase().endsWith('.wav')) {
      try {
        const bytes = await doc.file.arrayBuffer()
        const decoded = await runAnalysisJob({ kind: 'decode', bytes }, [bytes])
        return { ...decoded, savesInPlace: true }
      } catch {
        // Not a WAV the parser reads: the browser may still decode it below.
      }
    }
    return { ...(await decodeAudioFile(doc.file)), peaks: undefined }
  }

  async function decode(doc: EditorDocument): Promise<Channels> {
    try {
      const decoded = await decodeOffPage(doc)
      // Another caller may have filled it while this one was decoding.
      if (doc.channels) return doc.channels
      doc.sampleRate = decoded.sampleRate
      doc.layout = markRaw(decoded.layout)
      doc.savesInPlace = decoded.savesInPlace
      setChannels(doc, decoded.channels, decoded.peaks)
      adoptMarkers(doc)
      doc.status = 'ready'
      doc.error = null
      return decoded.channels
    } catch (error) {
      doc.status = 'error'
      doc.error = error instanceof Error ? error.message : 'This file could not be decoded.'
      throw error
    }
  }

  function setChannels(doc: EditorDocument, channels: Channels, peaks?: PeakPyramid) {
    doc.channels = markRaw(channels)
    doc.peaks = markRaw(peaks ?? buildPeaks(channels))
    doc.channelCount = channels.length
    doc.frames = frameCount(channels)
    doc.cursor = Math.min(doc.cursor, doc.frames)
    if (doc.selection) {
      const start = Math.min(doc.selection.start, doc.frames)
      const end = Math.min(doc.selection.end, doc.frames)
      doc.selection = end > start ? { start, end } : null
    }
  }

  /** Keeps PCM only for the active file, its neighbours and unsaved files. */
  function evictFarDocuments() {
    const list = visibleDocuments.value
    const index = list.findIndex((doc) => doc.id === activeId.value)
    const keep = new Set([list[index - 1]?.id, list[index]?.id, list[index + 1]?.id])
    for (const doc of documents.value) {
      if (doc.channels && !keep.has(doc.id) && !isDirty(doc) && !doc.analyzing) doc.channels = null
    }
  }

  function setAnalysis(doc: EditorDocument, analysis: AudioAnalysis) {
    doc.analysis = analysis
    doc.analysisState = doc.state
  }

  async function refreshAnalysis(doc: EditorDocument) {
    const channels = doc.channels
    if (!channels) return
    const state = doc.state
    doc.analyzing = true
    try {
      const analysis = await runAnalysisJob({
        kind: 'analyze',
        channels,
        sampleRate: doc.sampleRate,
      })
      if (doc.state === state) setAnalysis(doc, analysis)
    } finally {
      doc.analyzing = false
    }
  }

  function setActive(id: number) {
    activeId.value = id
    const list = visibleDocuments.value
    const index = list.findIndex((doc) => doc.id === id)
    for (const doc of [list[index], list[index - 1], list[index + 1]])
      if (doc && !doc.channels && doc.status !== 'error') void ensureChannels(doc).catch(() => {})
    evictFarDocuments()
  }

  function step(offset: number) {
    const list = visibleDocuments.value
    if (list.length === 0) return
    const index = list.findIndex((doc) => doc.id === activeId.value)
    const next = list[Math.max(0, Math.min(list.length - 1, (index < 0 ? 0 : index) + offset))]
    if (next) setActive(next.id)
  }

  /** Memory the undo history holds on to: the audio each step's patch keeps. */
  function undoBytes() {
    let total = 0
    for (const doc of documents.value)
      for (const snapshot of doc.undo) total += patchBytes(snapshot.audio)
    for (const doc of documents.value)
      for (const snapshot of doc.redo) total += patchBytes(snapshot.audio)
    return total
  }

  /**
   * Drops the oldest step of the deepest history until it fits, but leaves every file its latest
   * step: one whole-file step of a 2-hour take is over the budget on its own, and it should still
   * undo. Steps only keep what they changed, so this is reached by whole-file edits of long takes.
   */
  function trimUndoBudget() {
    while (undoBytes() > UNDO_BUDGET_BYTES) {
      const deepest = documents.value.reduce<EditorDocument | null>(
        (best, doc) => (doc.undo.length > (best?.undo.length ?? 1) ? doc : best),
        null,
      )
      if (!deepest) break
      deepest.undo.shift()
    }
  }

  function pushUndo(doc: EditorDocument, label: string, audio: AudioPatch | null) {
    doc.undo.push(
      markRaw({
        label,
        audio: audio && markRaw(audio),
        markers: doc.markers,
        state: doc.state,
        selection: doc.selection,
        spectralSelection: doc.spectralSelection,
        cursor: doc.cursor,
      }),
    )
    if (doc.undo.length > UNDO_LIMIT) doc.undo.shift()
    doc.redo = []
    trimUndoBudget()
  }

  /** `analysis` is for edits that already measured their output, saving a second pass. */
  function commit(
    doc: EditorDocument,
    label: string,
    previous: Channels,
    result: EditResult,
    analysis?: AudioAnalysis,
  ) {
    pushUndo(doc, label, diffPatch(previous, result.channels))
    const before = doc.frames
    setChannels(doc, result.channels)
    doc.state = nextState++
    if (result.splices)
      doc.markers = result.splices.reduce(
        (markers, splice) => spliceMarkers(markers, splice),
        doc.markers,
      )
    else if (doc.frames !== before) doc.markers = clampMarkers(doc.markers, doc.frames)
    // Audio moved under it: the areas it picked out are somewhere else now.
    if (doc.frames !== before) doc.spectralSelection = null
    if (result.selection !== undefined) doc.selection = result.selection
    if (result.cursor !== undefined) doc.cursor = Math.min(result.cursor, doc.frames)
    if (analysis) setAnalysis(doc, analysis)
    else void refreshAnalysis(doc)
  }

  /**
   * Shows `label` over the waveform while `task` runs. For long files it waits for the indicator to
   * paint first, since the work blocks the page; the spinner itself keeps turning regardless.
   */
  async function withBusy<T>(label: string, heavy: boolean, task: () => Promise<T> | T) {
    const previous = busy.value
    busy.value = label
    try {
      if (heavy) await nextPaint()
      return await task()
    } finally {
      busy.value = previous
    }
  }

  function isHeavy(doc: EditorDocument) {
    return doc.frames * Math.max(1, doc.channelCount) > HEAVY_SAMPLES
  }

  /** Runs `task` over the documents with one in flight per worker, reporting progress. */
  async function forEachInParallel(
    docs: EditorDocument[],
    task: (doc: EditorDocument) => Promise<void>,
    onProgress?: (done: number) => void,
  ) {
    let next = 0
    let done = 0
    const lane = async () => {
      while (next < docs.length) {
        const doc = docs[next++]!
        await task(doc)
        onProgress?.(++done)
      }
    }
    await Promise.all(Array.from({ length: Math.min(WORKER_COUNT, docs.length) }, lane))
  }

  /** Applies a destructive edit to one document as a single undo step; null leaves it untouched. */
  async function apply(
    doc: EditorDocument,
    label: string,
    edit: (channels: Channels, doc: EditorDocument) => EditResult | Channels | null,
  ) {
    const channels = await ensureChannels(doc)
    await withBusy(`${label}…`, isHeavy(doc), () => {
      const result = edit(channels, doc)
      if (!result) return
      commit(doc, label, channels, Array.isArray(result) ? { channels: result } : result)
    })
  }

  /**
   * Runs a cleanup step or spectral edit in a worker. With a selection (or a spectral area) only it
   * and a second either side go over, so previewing a de-esser on one word of a long take does not
   * copy the whole take.
   */
  async function renderStep(
    channels: Channels,
    sampleRate: number,
    step: ProcessStep,
    selection: FrameRange | null,
  ) {
    const range = stepRange(step, selection)
    if (!range) {
      const result = await runAnalysisJob({ kind: 'step', channels, sampleRate, step, selection })
      return { channels: markRaw(result.channels), clicks: result.clicks }
    }
    const frames = frameCount(channels)
    const pad = stepReach(step, sampleRate, STEP_PAD_SECONDS)
    const from = Math.max(0, range.start - pad)
    const to = Math.min(frames, range.end + pad)
    const slice = channels.map((channel) => channel.slice(from, to))
    const result = await runAnalysisJob(
      {
        kind: 'step',
        channels: slice,
        sampleRate,
        step: offsetStep(step, -from),
        selection: { start: range.start - from, end: range.end - from },
      },
      slice.map((channel) => channel.buffer),
    )
    // Soloing an area silences everything else, not just the slice around it.
    const silent = step.kind === 'spectral' && step.edit.mode === 'isolate'
    const output = channels.map((channel, index) => {
      const copy = silent ? new Float32Array(channel.length) : channel.slice()
      copy.set(result.channels[index]!, from)
      return copy
    })
    return { channels: markRaw(output), clicks: result.clicks }
  }

  function previewMatches(
    render: PreviewRender | null,
    doc: EditorDocument,
    channels: Channels,
    step: ProcessStep,
  ): render is PreviewRender {
    return (
      !!render &&
      render.docId === doc.id &&
      render.channels === channels &&
      render.step === step &&
      sameRange(render.selection, doc.selection)
    )
  }

  /** What playback should play: the audio, or the previewed step's render once it is ready. */
  function playbackChannels(doc: EditorDocument, channels: Channels): Channels {
    const step = preview.value
    if (!step || previewBypass.value) return channels
    const render = previewRender.value
    return previewMatches(render, doc, channels, step) ? render.output : channels
  }

  let previewLoop = false

  /** Renders the previewed step for the open file, again whenever it moved on meanwhile. */
  async function renderPreview() {
    if (previewLoop) return
    previewLoop = true
    try {
      for (;;) {
        const doc = activeDocument.value
        const step = preview.value
        const channels = doc?.channels
        if (!doc || !step || !channels || previewMatches(previewRender.value, doc, channels, step))
          break
        previewRendering.value = true
        const selection = doc.selection
        let render: PreviewRender
        try {
          const result = await renderStep(channels, doc.sampleRate, step, selection)
          render = {
            docId: doc.id,
            channels,
            step,
            selection,
            output: result.channels,
            clicks: result.clicks,
          }
        } catch {
          // A noise print at another rate: the panel says so; play the audio as it is.
          render = { docId: doc.id, channels, step, selection, output: channels, clicks: 0 }
        }
        previewRender.value = markRaw(render)
      }
    } finally {
      previewRendering.value = false
      previewLoop = false
    }
  }

  watchDebounced(
    () => [
      preview.value,
      activeDocument.value?.channels,
      activeDocument.value?.selection?.start,
      activeDocument.value?.selection?.end,
    ],
    () => {
      if (!preview.value) previewRender.value = null
      else void renderPreview()
    },
    { debounce: 150 },
  )

  /**
   * Applies a cleanup step to the selection (or file) as one undo step. Reuses the preview's
   * render when it is of this very audio and step, so what was heard is exactly what is applied.
   */
  async function applyStep(
    doc: EditorDocument,
    step: ProcessStep,
    label: (clicks: number) => string,
  ) {
    const channels = await ensureChannels(doc)
    return withBusy(`${describeStep(step)}…`, false, async () => {
      const render = previewRender.value
      const result = previewMatches(render, doc, channels, step)
        ? render
        : await renderStep(channels, doc.sampleRate, step, doc.selection)
      if (step.kind === 'clicks' && result.clicks === 0) return 0
      // Edited while the worker ran: the result would overwrite that edit.
      if (doc.channels !== channels) throw new Error('The file changed while this was running.')
      if (isHeavy(doc)) await nextPaint()
      commit(doc, label(result.clicks), channels, { channels: result.channels })
      return result.clicks
    })
  }

  /** Copy: the selection, or the whole file when nothing is selected. */
  async function copy(doc: EditorDocument) {
    const channels = await ensureChannels(doc)
    clipboard.value = markRaw({
      channels: doc.selection ? crop(channels, doc.selection) : channels.map((c) => c.slice()),
      sampleRate: doc.sampleRate,
      source: doc.name,
    })
  }

  async function cut(doc: EditorDocument) {
    const selection = doc.selection
    if (!selection) return
    await copy(doc)
    await apply(doc, 'Cut', (channels) => ({
      channels: deleteRange(channels, selection),
      selection: null,
      cursor: selection.start,
      splices: [{ start: selection.start, removed: selection.end - selection.start, inserted: 0 }],
    }))
  }

  /** Paste inserts at the cursor, or replaces the selection, and selects what was pasted. */
  async function paste(doc: EditorDocument) {
    const clip = clipboard.value
    if (!clip) throw new Error('Nothing has been copied yet.')
    await apply(doc, 'Paste', (channels, target) => {
      const pasted = conformClip(clip, channels.length, target.sampleRate)
      const start = target.selection?.start ?? target.cursor
      const length = frameCount(pasted)
      const removed = target.selection ? target.selection.end - target.selection.start : 0
      return {
        channels: pasteInsert(channels, pasted, target.cursor, target.selection),
        selection: length > 0 ? { start, end: start + length } : null,
        cursor: start,
        splices: [{ start, removed, inserted: length }],
      }
    })
  }

  /** Mix Paste lays the clipboard over the audio from the cursor (or the selection's start). */
  async function mixPaste(doc: EditorDocument) {
    const clip = clipboard.value
    if (!clip) throw new Error('Nothing has been copied yet.')
    const settings = mixPasteSettings.value
    await apply(doc, 'Mix paste', (channels, target) => {
      const pasted = conformClip(clip, channels.length, target.sampleRate)
      const start = Math.min(target.selection?.start ?? target.cursor, target.frames)
      const length = frameCount(pasted)
      return {
        channels: mixPasteClip(channels, pasted, start, {
          mode: settings.mode,
          volumeDb: settings.volumeDb,
          crossfadeFrames: (settings.crossfadeMs / 1000) * target.sampleRate,
        }),
        selection: length > 0 ? { start, end: start + length } : null,
        cursor: start,
      }
    })
  }

  function adjustSelectionToZeroCrossings(doc: EditorDocument, adjustment: ZeroCrossingAdjustment) {
    if (!doc.selection || !doc.channels) return
    const reach = Math.round(doc.sampleRate * ZERO_CROSSING_REACH_SECONDS)
    const next = adjustToZeroCrossings(doc.channels, doc.selection, adjustment, reach)
    if (!next) return
    doc.selection = next
    doc.cursor = next.start
  }

  /** Sets the selection and cursor, moving them to zero crossings when snapping is on. */
  function select(doc: EditorDocument, selection: FrameRange | null, cursor: number) {
    // Picking another stretch of time drops the spectral areas; moving only the cursor keeps them.
    if (!sameRange(selection, doc.selection)) doc.spectralSelection = null
    const channels = doc.channels
    if (snapToZeroCrossings.value && channels) {
      const reach = Math.round(doc.sampleRate * SNAP_REACH_SECONDS)
      const snap = (frame: number) => zeroCrossing(channels, frame, 'nearest', reach)
      if (selection) {
        const start = snap(selection.start)
        const end = snap(selection.end)
        cursor = cursor === selection.start ? start : snap(cursor)
        selection = end > start ? { start, end } : null
      } else {
        cursor = snap(cursor)
      }
    }
    doc.selection = selection
    doc.cursor = cursor
  }

  /** Opens audio as a new, unsaved file; saving it asks where to put it. */
  function createDocument(channels: Channels, sampleRate: number) {
    const known = new Set(documents.value.map((doc) => doc.path))
    let name = `Untitled ${nextUntitled++}.wav`
    while (known.has(name)) name = `Untitled ${nextUntitled++}.wav`
    const bytes = encodeWav(channels, sampleRate, defaultLayout(channels.length, 'float'))
    addDocuments([
      { file: new File([bytes], name, { type: 'audio/wav' }), handle: null, path: name },
    ])
    const created = documents.value.find((doc) => doc.path === name)
    if (!created) return
    // Never on disk, so it counts as unsaved until it is.
    created.savedState = 0
    setActive(created.id)
  }

  /** Copy to New: the selection (or whole file) becomes a file of its own. */
  async function copyToNew(doc: EditorDocument) {
    const channels = await ensureChannels(doc)
    createDocument(doc.selection ? crop(channels, doc.selection) : channels, doc.sampleRate)
  }

  function restore(doc: EditorDocument, from: Snapshot[], to: Snapshot[]) {
    const snapshot = from.at(-1)
    if (!snapshot) return
    // Evicted samples: keep the step for when they are back rather than dropping it.
    if (snapshot.audio && !doc.channels) return
    from.pop()
    const patched = snapshot.audio ? applyPatch(doc.channels!, snapshot.audio) : null
    to.push(
      markRaw({
        label: snapshot.label,
        audio: patched && markRaw(patched.redo),
        markers: doc.markers,
        state: doc.state,
        selection: doc.selection,
        spectralSelection: doc.spectralSelection,
        cursor: doc.cursor,
      }),
    )
    if (patched) setChannels(doc, patched.channels)
    doc.markers = snapshot.markers
    doc.state = snapshot.state
    doc.selection = snapshot.selection
    doc.spectralSelection = snapshot.spectralSelection
    doc.cursor = snapshot.cursor
    if (patched) void refreshAnalysis(doc)
  }

  function undo(doc: EditorDocument) {
    restore(doc, doc.undo, doc.redo)
  }

  function redo(doc: EditorDocument) {
    restore(doc, doc.redo, doc.undo)
  }

  /** The file as it would be written: its audio, its metadata chunks and its markers. */
  async function encode(doc: EditorDocument) {
    const channels = await ensureChannels(doc)
    return withBusy(`Encoding ${doc.name}…`, isHeavy(doc), () => {
      const layout = withMarkers(
        doc.layout ?? defaultLayout(channels.length),
        doc.markers,
        doc.sampleRate,
      )
      return new Blob([encodeWav(channels, doc.sampleRate, layout)], { type: 'audio/wav' })
    })
  }

  function markSaved(doc: EditorDocument, blob: Blob) {
    doc.file = new File([blob], doc.name, { type: 'audio/wav', lastModified: Date.now() })
    doc.savedState = doc.state
  }

  /**
   * Ctrl+S: writes over the original when the browser gave us a handle to it. Compressed sources,
   * files without a handle and Save As go through the save picker, or a download where there is
   * no picker. Returns false when the person cancelled.
   */
  async function save(doc: EditorDocument, mode: 'save' | 'saveAs' = 'save') {
    const blob = await encode(doc)
    if (mode === 'save' && doc.handle && doc.savesInPlace) {
      await writeFile(doc.handle, blob)
    } else if (canWriteInPlace()) {
      const handle = await saveAs(withWavExtension(doc.name), blob)
      if (!handle) return false
      doc.handle = handle
      if (handle.name !== doc.name) {
        doc.name = handle.name
        doc.path = doc.path.includes('/')
          ? `${doc.path.slice(0, doc.path.lastIndexOf('/'))}/${handle.name}`
          : handle.name
      }
      if (!doc.layout || !doc.savesInPlace) doc.layout = markRaw(defaultLayout(doc.channelCount))
      doc.savesInPlace = true
    } else {
      download(withWavExtension(doc.name), blob)
    }
    markSaved(doc, blob)
    return true
  }

  /** `markAsSaved` is for Save all's fallback; a plain export leaves the originals unsaved. */
  async function zipDocuments(docs: EditorDocument[], markAsSaved: boolean) {
    const entries = []
    const taken = new Set<string>()
    for (const doc of docs) {
      const blob = await encode(doc)
      entries.push({
        name: uniqueName(withWavExtension(doc.path), taken),
        data: new Uint8Array(await blob.arrayBuffer()),
      })
      if (markAsSaved) markSaved(doc, blob)
    }
    return createZip(entries)
  }

  /**
   * Writes every unsaved file (of `docs`, or all) it holds a handle for over its original, and
   * bundles the rest into one ZIP download rather than opening a save dialog per file.
   */
  async function saveAll(docs: EditorDocument[] = documents.value) {
    const dirty = docs.filter(isDirty)
    const inPlace = dirty.filter((doc) => doc.handle && doc.savesInPlace)
    const rest = dirty.filter((doc) => !inPlace.includes(doc))
    for (const doc of inPlace) {
      const blob = await encode(doc)
      await writeFile(doc.handle!, blob)
      markSaved(doc, blob)
    }
    if (rest.length > 0) download('voices-of-wynn-audio.zip', await zipDocuments(rest, true))
    return { written: inPlace.length, zipped: rest.length }
  }

  async function exportZip(docs: EditorDocument[]) {
    download('voices-of-wynn-audio.zip', await zipDocuments(docs, false))
  }

  async function analyzeAll(docs: EditorDocument[], onProgress?: (done: number) => void) {
    await forEachInParallel(
      docs,
      async (doc) => {
        if (doc.status === 'loading') await awaitLoad(doc)
        // Being measured already: that run fills in its analysis.
        if (doc.analysis || doc.analyzing || doc.status !== 'ready') return
        if (!doc.channels) await initialLoad(doc)
        else await refreshAnalysis(doc)
      },
      onProgress,
    )
  }

  /** The threshold a trim of this file uses: its own override, else its suggestion, else the VoW rule. */
  function trimThresholdFor(doc: EditorDocument) {
    return doc.trimThresholdDb ?? doc.analysis?.silence.suggestedThresholdDb ?? SILENCE_THRESHOLD_DB
  }

  function setTrimThreshold(doc: EditorDocument, thresholdDb: number | null) {
    doc.trimThresholdDb = thresholdDb
  }

  /** What a trim would cut right now, for the preview. Null until the samples are loaded. */
  function trimPlanFor(doc: EditorDocument): TrimPlan | null {
    if (!doc.channels) return null
    return planTrim(doc.channels, doc.sampleRate, {
      thresholdDb: trimThresholdFor(doc),
      keepLeadSeconds: trimSettings.value.keepLeadMs / 1000,
      keepTailSeconds: trimSettings.value.keepTailMs / 1000,
    })
  }

  /** Trims every file on the worker pool, each with its own threshold. */
  async function trimAll(docs: EditorDocument[], onProgress?: (done: number) => void) {
    let trimmed = 0
    await forEachInParallel(
      docs,
      async (doc) => {
        if (doc.status === 'error') return
        const channels = await ensureChannels(doc)
        const state = doc.state
        const result = await runAnalysisJob({
          kind: 'trim',
          channels,
          sampleRate: doc.sampleRate,
          keepLeadSeconds: trimSettings.value.keepLeadMs / 1000,
          keepTailSeconds: trimSettings.value.keepTailMs / 1000,
          thresholdDb: doc.trimThresholdDb ?? undefined,
        })
        // Edited while the worker ran: the result would overwrite that edit.
        if (!result.plan || doc.state !== state) return
        const { start, end } = result.plan.keep
        commit(
          doc,
          `Trim silence below ${Math.round(result.thresholdDb)} dB`,
          channels,
          {
            channels: result.channels,
            selection: null,
            cursor: 0,
            splices: cropSplices(doc.frames, { start, end }),
          },
          result.analysis,
        )
        trimmed++
      },
      onProgress,
    )
    return { trimmed }
  }

  async function matchAll(
    docs: EditorDocument[],
    options: MatchOptions,
    onProgress?: (done: number) => void,
  ) {
    let limited = 0
    /** Files the limiter had to flatten hard, and files that could not reach the target. */
    const heavy: string[] = []
    const short: { name: string; lufs: number }[] = []
    await forEachInParallel(
      docs,
      async (doc) => {
        if (doc.status === 'error') return
        const channels = await ensureChannels(doc)
        const state = doc.state
        // An analysis of earlier audio would give the wrong gain; the worker measures afresh.
        const before =
          doc.analysis && doc.analysisState === state
            ? // A reactive proxy cannot be structured-cloned into the worker.
              { ...toRaw(doc.analysis) }
            : undefined
        const result = await runAnalysisJob({
          kind: 'match',
          channels,
          sampleRate: doc.sampleRate,
          options: { ...options },
          before,
        })
        if (doc.state !== state) return
        if (result.gainDb !== 0 || result.limited)
          commit(
            doc,
            `Match loudness to ${options.targetLufs} LUFS`,
            channels,
            { channels: result.channels },
            result.after,
          )
        if (result.limited) limited++
        if (result.limitingDb > HEAVY_LIMITING_DB) heavy.push(doc.name)
        if (result.after.integratedLufs < options.targetLufs - SHORTFALL_LU)
          short.push({ name: doc.name, lufs: result.after.integratedLufs })
      },
      onProgress,
    )
    return { limited, heavy, short }
  }

  /** Runs the steps over whole files on the worker pool, each file as one undo step. */
  async function cleanupAll(
    docs: EditorDocument[],
    steps: CleanupStep[],
    onProgress?: (done: number) => void,
  ) {
    const label = steps.map(describeStep).join(', ')
    let processed = 0
    const skipped = new Map<CleanupKind, number>()
    await forEachInParallel(
      docs,
      async (doc) => {
        if (doc.status === 'error') return
        const channels = await ensureChannels(doc)
        const state = doc.state
        const result = await runAnalysisJob({
          kind: 'cleanup',
          channels,
          sampleRate: doc.sampleRate,
          steps,
        })
        for (const kind of result.skipped) skipped.set(kind, (skipped.get(kind) ?? 0) + 1)
        if (result.skipped.length === steps.length || doc.state !== state) return
        commit(doc, label, channels, { channels: result.channels }, result.analysis)
        processed++
      },
      onProgress,
    )
    return { processed, skipped }
  }

  /**
   * Replaces the markers as one undo step. `before` is what undo goes back to.
   */
  function setMarkers(doc: EditorDocument, label: string, markers: Marker[], before = doc.markers) {
    doc.markers = before
    pushUndo(doc, label, null)
    doc.markers = sortMarkers(markers)
    doc.state = nextState++
  }

  /**
   * Ctrl+B: a range marker over the selection, or a cue marker at the cursor without one, like
   * Audition's Add Marker. Returns the new marker.
   */
  function addMarker(doc: EditorDocument, data?: Partial<MarkerData>) {
    const selection = doc.selection
    const marker: Marker = {
      id: nextMarkerId++,
      name: data?.name ?? nextMarkerName(doc.markers),
      start: data?.start ?? selection?.start ?? doc.cursor,
      length: data?.length ?? (selection ? selection.end - selection.start : 0),
      comment: data?.comment ?? '',
      guid: null,
    }
    setMarkers(doc, marker.length > 0 ? 'Add range marker' : 'Add marker', [...doc.markers, marker])
    doc.selectedMarkerId = marker.id
    return marker
  }

  function updateMarker(
    doc: EditorDocument,
    id: number,
    patch: Partial<MarkerData>,
    label: string,
  ) {
    const index = doc.markers.findIndex((marker) => marker.id === id)
    if (index < 0) return
    const next = doc.markers.slice()
    next[index] = { ...next[index]!, ...patch }
    setMarkers(doc, label, next)
  }

  function removeMarkers(doc: EditorDocument, ids: number[]) {
    const removing = new Set(ids)
    const kept = doc.markers.filter((marker) => !removing.has(marker.id))
    if (kept.length === doc.markers.length) return
    setMarkers(doc, removing.size === 1 ? 'Delete marker' : `Delete ${removing.size} markers`, kept)
    if (doc.selectedMarkerId !== null && removing.has(doc.selectedMarkerId))
      doc.selectedMarkerId = null
  }

  /** Marks a marker as the one being worked on (in the list and on the ruler) without selecting it. */
  function highlightMarker(doc: EditorDocument, id: number) {
    doc.selectedMarkerId = id
  }

  /** Splits a range in two at `frame`; the second half is named after the first. */
  function splitMarker(doc: EditorDocument, id: number, frame: number) {
    const marker = doc.markers.find((entry) => entry.id === id)
    if (!marker || frame <= marker.start || frame >= marker.start + marker.length) return
    const second: Marker = {
      ...marker,
      id: nextMarkerId++,
      name: `${marker.name} (2)`,
      start: frame,
      length: marker.start + marker.length - frame,
      guid: null,
    }
    setMarkers(doc, 'Split marker', [
      ...doc.markers.filter((entry) => entry.id !== id),
      { ...marker, length: frame - marker.start },
      second,
    ])
  }

  /** Joins markers into the first one in time, spanning from the earliest start to the latest end. */
  function mergeMarkers(doc: EditorDocument, ids: number[]) {
    const merging = sortMarkers(doc.markers.filter((marker) => ids.includes(marker.id)))
    const first = merging[0]
    if (!first || merging.length < 2) return
    const end = Math.max(...merging.map((marker) => marker.start + marker.length))
    const merged: Marker = {
      ...first,
      length: end - first.start,
      comment: merging
        .map((marker) => marker.comment)
        .filter(Boolean)
        .join(' '),
    }
    setMarkers(doc, `Merge ${merging.length} markers`, [
      ...doc.markers.filter((marker) => !ids.includes(marker.id)),
      merged,
    ])
    doc.selectedMarkerId = first.id
  }

  /** Picks a marker: selects its range (or puts the cursor on it) and keeps it in view. */
  function selectMarker(doc: EditorDocument, id: number) {
    const marker = doc.markers.find((entry) => entry.id === id)
    if (!marker) return
    doc.selectedMarkerId = id
    doc.spectralSelection = null
    doc.selection =
      marker.length > 0 ? { start: marker.start, end: marker.start + marker.length } : null
    doc.cursor = marker.start
  }

  /**
   * Writes each range out as a WAV of its own, in the source's format and without the take's
   * metadata. Names are given by the caller, in the same order as the markers.
   */
  async function exportMarkers(doc: EditorDocument, options: MarkerExport) {
    const channels = await ensureChannels(doc)
    const format = doc.layout ?? defaultLayout(channels.length)
    const files: File[] = []
    try {
      for (const [index, marker] of options.markers.entries()) {
        busy.value = `Exporting ${index + 1} / ${options.markers.length}…`
        // Let the indicator move every few files; each one is quick on its own.
        if (index % 8 === 0) await nextPaint()
        const range = {
          start: marker.start,
          end: Math.min(doc.frames, marker.start + marker.length),
        }
        const layout = defaultLayout(channels.length, format.format, format.bitDepth)
        const bytes = encodeWav(crop(channels, range), doc.sampleRate, layout)
        const file = new File([bytes], options.names[index]!, { type: 'audio/wav' })
        if (options.target === 'folder') {
          const handle = await options.directory!.getFileHandle(file.name, { create: true })
          await writeFile(handle, file)
        } else {
          files.push(file)
        }
      }
      if (options.target === 'zip') {
        busy.value = 'Building ZIP…'
        await nextPaint()
        const entries = await Promise.all(
          files.map(async (file) => ({
            name: file.name,
            data: new Uint8Array(await file.arrayBuffer()),
          })),
        )
        download(options.zipName ?? 'markers.zip', createZip(entries))
      } else if (options.target === 'open' && files.length > 0) {
        addDocuments(files.map((file) => ({ file, handle: null, path: file.name })))
        // Never on disk, so they count as unsaved until they are.
        for (const entry of documents.value.slice(-files.length)) entry.savedState = 0
      }
    } finally {
      busy.value = null
    }
    return options.markers.length
  }

  /**
   * Sets the spectral selection. The time selection follows the span of its areas, so Space plays
   * them and the status bar times them; clearing it leaves the time selection be.
   */
  function setSpectralSelection(doc: EditorDocument, selection: SpectralSelection | null) {
    const bounds = selectionBounds(selection)
    if (!selection || !bounds) {
      doc.spectralSelection = null
      return
    }
    doc.spectralSelection = markRaw(selection)
    const start = Math.max(0, Math.min(doc.frames, Math.floor(bounds.start)))
    const end = Math.max(0, Math.min(doc.frames, Math.ceil(bounds.end)))
    doc.selection = end > start ? { start, end } : null
    doc.cursor = start
  }

  /** A spectral edit of `selection` with the panel's settings, or null with nothing selected. */
  function spectralStep(
    mode: SpectralMode,
    selection: SpectralSelection | null,
  ): SpectralStep | null {
    if (!selection || !selectionBounds(selection)) return null
    const settings = spectralSettings.value
    return markRaw<SpectralStep>({
      kind: 'spectral',
      edit: {
        selection,
        mode,
        gainDb: settings.gainDb,
        feather: settings.feather / 100,
        direction: settings.healDirection,
        fftSize: settings.fftSize,
      },
    })
  }

  /** Applies a spectral edit to the spectral selection (or `selection`) as one undo step. */
  async function applySpectral(
    doc: EditorDocument,
    mode: SpectralMode,
    selection = doc.spectralSelection,
  ) {
    const step = spectralStep(mode, selection)
    if (!step) return false
    const problem = stepProblem(step, doc.sampleRate)
    if (problem) throw new Error(problem)
    await applyStep(doc, step, () => describeStep(step))
    return true
  }

  /** Copies just the spectral selection, everything around it silenced, into a new file. */
  async function extractSpectral(doc: EditorDocument) {
    const step = spectralStep('isolate', doc.spectralSelection)
    const range = step && stepRange(step, null)
    if (!step || !range) return
    const channels = await ensureChannels(doc)
    await withBusy('Extracting…', false, async () => {
      const result = await renderStep(channels, doc.sampleRate, step, null)
      createDocument(crop(result.channels, range), doc.sampleRate)
    })
  }

  function close(doc: EditorDocument) {
    const list = visibleDocuments.value
    const index = list.findIndex((entry) => entry.id === doc.id)
    documents.value = documents.value.filter((entry) => entry.id !== doc.id)
    if (activeId.value === doc.id) {
      const next = list[index + 1] ?? list[index - 1]
      activeId.value = next && next.id !== doc.id ? next.id : null
      if (activeId.value !== null) setActive(activeId.value)
    }
  }

  /** Closes several at once, moving to the nearest file that stays open. */
  function closeMany(docs: EditorDocument[]) {
    const closing = new Set(docs.map((doc) => doc.id))
    if (closing.size === 0) return
    const list = visibleDocuments.value
    const index = list.findIndex((entry) => entry.id === activeId.value)
    documents.value = documents.value.filter((entry) => !closing.has(entry.id))
    for (let i = loadQueue.length - 1; i >= 0; i--)
      if (closing.has(loadQueue[i]!.id)) loadQueue.splice(i, 1)
    if (activeId.value === null || !closing.has(activeId.value)) return
    const next =
      list.slice(index + 1).find((entry) => !closing.has(entry.id)) ??
      list
        .slice(0, Math.max(0, index))
        .reverse()
        .find((entry) => !closing.has(entry.id))
    activeId.value = next?.id ?? null
    if (next) setActive(next.id)
  }

  function closeAll() {
    documents.value = []
    activeId.value = null
    loadQueue.length = 0
  }

  // Here rather than in the editor view: the files stay open on other pages, and closing or
  // reloading the tab from there loses them just the same.
  useEventListener(window, 'beforeunload', (event: BeforeUnloadEvent) => {
    if (dirtyDocuments.value.length === 0) return
    event.preventDefault()
    event.returnValue = ''
  })

  return reactive({
    documents,
    activeId,
    activeDocument,
    search,
    visibleDocuments,
    dirtyDocuments,
    checkedDocuments,
    batchDocuments,
    targetLufs,
    ceilingDbtp,
    peakControl,
    addFiles,
    ensureChannels,
    setActive,
    step,
    apply,
    undo,
    redo,
    save,
    saveAll,
    exportZip,
    analyzeAll,
    matchAll,
    clipboard,
    noisePrint,
    preview,
    previewBypass,
    previewRender,
    previewRendering,
    playbackChannels,
    applyStep,
    busy,
    setMarkers,
    addMarker,
    updateMarker,
    removeMarkers,
    selectMarker,
    highlightMarker,
    splitMarker,
    mergeMarkers,
    exportMarkers,
    cleanupAll,
    mixPasteSettings,
    snapToZeroCrossings,
    copy,
    cut,
    paste,
    mixPaste,
    copyToNew,
    adjustSelectionToZeroCrossings,
    select,
    spectralSettings,
    frequencyView,
    setSpectralSelection,
    spectralStep,
    applySpectral,
    extractSpectral,
    trimSettings,
    trimThresholdFor,
    setTrimThreshold,
    trimPlanFor,
    trimAll,
    close,
    closeMany,
    closeAll,
  })
}

let workspace: ReturnType<typeof createWorkspace> | null = null

/**
 * The open files, shared by every component of the editor and kept when navigating away and back.
 * Deliberately not a Pinia store: the Vue devtools record and serialise every store mutation, and
 * with hundreds of files each bringing back waveform peaks that made loading 20x slower in dev.
 */
export function useAudioWorkspace() {
  workspace ??= effectScope(true).run(createWorkspace)!
  return workspace
}

// Hot-swapping this module would leave components split between the old workspace and a new empty
// one, each with its own loads and workers in flight, so an edit here reloads the page instead.
if (import.meta.hot) import.meta.hot.accept(() => window.location.reload())
