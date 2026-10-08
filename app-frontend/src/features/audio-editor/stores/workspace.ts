import { useEventListener, useLocalStorage } from '@vueuse/core'
import { computed, effectScope, markRaw, reactive, ref, toRaw } from 'vue'
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
  runCleanupStep,
  type CleanupKind,
  type CleanupStep,
} from '../lib/cleanupSteps'
import type { AudioAnalysis } from '../lib/loudness'
import type { NoisePrint } from '../lib/noiseReduction'
import { planTrim, type TrimPlan } from '../lib/silence'
import { crop, deleteRange, frameCount, type Channels, type FrameRange } from '../lib/operations'
import { buildPeaks, type PeakPyramid } from '../lib/peaks'
import type { Spectrogram } from '../lib/spectrogram'
import { defaultLayout, encodeWav, type WavLayout } from '../lib/wav'
import { createZip } from '../lib/zip'
import { SILENCE_THRESHOLD_DB } from '@/features/tools/lib/audioChecks'

interface Snapshot {
  label: string
  channels: Channels
  state: number
  selection: FrameRange | null
  cursor: number
}

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
  spectrogram: Spectrogram | null
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
  cursor: number
  view: { start: number; samplesPerPixel: number } | null
  checked: boolean
  /** Silence threshold set on this file; null follows its suggested one. */
  trimThresholdDb: number | null
}

export interface EditResult {
  channels: Channels
  selection?: FrameRange | null
  cursor?: number
}

/** How far Edit > Zero Crossings looks, and how far snapping may move a dragged edge. */
const ZERO_CROSSING_REACH_SECONDS = 0.1
const SNAP_REACH_SECONDS = 0.02
const UNDO_LIMIT = 50
const UNDO_BUDGET_BYTES = 512 * 1024 * 1024
// Two in flight per worker, so each has the next file read from disk by the time it finishes.
const LOAD_CONCURRENCY = WORKER_COUNT * 2

export function isDirty(doc: EditorDocument) {
  return doc.state !== doc.savedState
}

function snapshotBytes(snapshot: Snapshot) {
  return snapshot.channels.reduce((sum, channel) => sum + channel.byteLength, 0)
}

function createWorkspace() {
  const documents = ref<EditorDocument[]>([])
  const activeId = ref<number | null>(null)
  const search = ref('')
  const targetLufs = useLocalStorage('vow.audioEditor.targetLufs', -18)
  const ceilingDbtp = useLocalStorage('vow.audioEditor.ceilingDbtp', -1)
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
  const preview = ref<CleanupStep | null>(null)
  /** A/B: play the untouched audio while a preview is set. */
  const previewBypass = ref(false)
  let previewCache: {
    channels: Channels
    step: CleanupStep
    selection: FrameRange | null
    output: Channels
  } | null = null
  let nextId = 1
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
  let loading = 0

  function pumpLoads() {
    while (loading < LOAD_CONCURRENCY && loadQueue.length > 0) {
      const doc = loadQueue.shift()!
      loading++
      void initialLoad(doc).finally(() => {
        loading--
        pumpLoads()
      })
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

  function addFiles(opened: OpenedFile[]) {
    const known = new Set(documents.value.map((doc) => doc.path))
    const added: EditorDocument[] = []
    for (const entry of opened) {
      if (known.has(entry.path)) continue
      known.add(entry.path)
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
        spectrogram: null,
        analysis: null,
        analysisState: 0,
        analyzing: false,
        undo: [],
        redo: [],
        state,
        savedState: state,
        selection: null,
        cursor: 0,
        view: null,
        checked: false,
        trimThresholdDb: null,
      })
      // Read back through the reactive array so later writes are tracked.
      added.push(documents.value[documents.value.length - 1]!)
    }
    if (activeId.value === null && added[0]) activeId.value = added[0].id
    loadQueue.push(...added)
    pumpLoads()
    return added.length
  }

  async function ensureChannels(doc: EditorDocument): Promise<Channels> {
    if (doc.channels) return doc.channels
    try {
      const decoded = await decodeAudioFile(doc.file)
      // Another caller may have filled it while this one was decoding.
      if (doc.channels) return doc.channels
      doc.sampleRate = decoded.sampleRate
      doc.layout = markRaw(decoded.layout)
      doc.savesInPlace = decoded.savesInPlace
      setChannels(doc, decoded.channels)
      doc.status = 'ready'
      doc.error = null
      return decoded.channels
    } catch (error) {
      doc.status = 'error'
      doc.error = error instanceof Error ? error.message : 'This file could not be decoded.'
      throw error
    }
  }

  function setChannels(doc: EditorDocument, channels: Channels) {
    doc.channels = markRaw(channels)
    doc.peaks = markRaw(buildPeaks(channels))
    doc.spectrogram = null
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
      if (doc.channels && !keep.has(doc.id) && !isDirty(doc) && !doc.analyzing) {
        doc.channels = null
        doc.spectrogram = null
      }
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

  async function ensureSpectrogram(doc: EditorDocument) {
    if (doc.spectrogram) return doc.spectrogram
    const channels = await ensureChannels(doc)
    const state = doc.state
    const spectrogram = await runAnalysisJob({
      kind: 'spectrogram',
      channels,
      sampleRate: doc.sampleRate,
    })
    if (doc.state === state && doc.channels) doc.spectrogram = markRaw(spectrogram)
    return spectrogram
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

  function trimUndoBudget() {
    let total = 0
    for (const doc of documents.value)
      for (const snapshot of [...doc.undo, ...doc.redo]) total += snapshotBytes(snapshot)
    // Drop the oldest step of the deepest history until it fits.
    while (total > UNDO_BUDGET_BYTES) {
      const deepest = documents.value.reduce<EditorDocument | null>(
        (best, doc) => (doc.undo.length > (best?.undo.length ?? 0) ? doc : best),
        null,
      )
      const dropped = deepest?.undo.shift()
      if (!dropped) break
      total -= snapshotBytes(dropped)
    }
  }

  function pushUndo(doc: EditorDocument, label: string, channels: Channels) {
    doc.undo.push(
      markRaw({ label, channels, state: doc.state, selection: doc.selection, cursor: doc.cursor }),
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
    pushUndo(doc, label, previous)
    setChannels(doc, result.channels)
    doc.state = nextState++
    if (result.selection !== undefined) doc.selection = result.selection
    if (result.cursor !== undefined) doc.cursor = Math.min(result.cursor, doc.frames)
    if (analysis) setAnalysis(doc, analysis)
    else void refreshAnalysis(doc)
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
    const result = edit(channels, doc)
    if (!result) return
    commit(doc, label, channels, Array.isArray(result) ? { channels: result } : result)
  }

  /**
   * What playback should play for the document: its audio, or the previewed step run over it. The
   * render is cached, so asking again for the same audio, step and selection costs nothing.
   */
  function playbackChannels(doc: EditorDocument, channels: Channels): Channels {
    const step = preview.value
    if (!step || previewBypass.value) return channels
    const cached = previewCache
    if (
      cached &&
      cached.channels === channels &&
      cached.step === step &&
      cached.selection?.start === doc.selection?.start &&
      cached.selection?.end === doc.selection?.end
    )
      return cached.output
    let output: Channels
    try {
      output = markRaw(runCleanupStep(channels, doc.sampleRate, step, doc.selection).channels)
    } catch {
      // A noise print at another rate: the panel says so; play the audio as it is.
      output = channels
    }
    previewCache = { channels, step, selection: doc.selection, output }
    return output
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
      return {
        channels: pasteInsert(channels, pasted, target.cursor, target.selection),
        selection: length > 0 ? { start, end: start + length } : null,
        cursor: start,
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
    addFiles([{ file: new File([bytes], name, { type: 'audio/wav' }), handle: null, path: name }])
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
    const snapshot = from.pop()
    if (!snapshot || !doc.channels) return
    to.push(
      markRaw({
        label: snapshot.label,
        channels: doc.channels,
        state: doc.state,
        selection: doc.selection,
        cursor: doc.cursor,
      }),
    )
    setChannels(doc, snapshot.channels)
    doc.state = snapshot.state
    doc.selection = snapshot.selection
    doc.cursor = snapshot.cursor
    void refreshAnalysis(doc)
  }

  function undo(doc: EditorDocument) {
    restore(doc, doc.undo, doc.redo)
  }

  function redo(doc: EditorDocument) {
    restore(doc, doc.redo, doc.undo)
  }

  async function encode(doc: EditorDocument) {
    const channels = await ensureChannels(doc)
    const layout = doc.layout ?? defaultLayout(channels.length)
    return new Blob([encodeWav(channels, doc.sampleRate, layout)], { type: 'audio/wav' })
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
    for (const doc of docs) {
      const blob = await encode(doc)
      entries.push({
        name: withWavExtension(doc.path),
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
        // Still loading or being measured: that run fills in its analysis.
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
        commit(
          doc,
          `Trim silence below ${Math.round(result.thresholdDb)} dB`,
          channels,
          { channels: result.channels, selection: null, cursor: 0 },
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
    target: number,
    ceiling: number,
    onProgress?: (done: number) => void,
  ) {
    let limited = 0
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
          targetLufs: target,
          ceilingDbtp: ceiling,
          before,
        })
        if (doc.state !== state) return
        if (result.gainDb !== 0 || result.limited)
          commit(
            doc,
            `Match loudness to ${target} LUFS`,
            channels,
            { channels: result.channels },
            result.after,
          )
        if (result.limited) limited++
      },
      onProgress,
    )
    return { limited }
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
    addFiles,
    ensureChannels,
    ensureSpectrogram,
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
    playbackChannels,
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
