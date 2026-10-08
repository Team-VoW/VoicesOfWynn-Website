import { watchThrottled } from '@vueuse/core'
import { markRaw, shallowRef, type Ref } from 'vue'
import { runAnalysisJob } from '../lib/analysisClient'
import type { Channels } from '../lib/operations'
import { buildPeaks, type PeakPyramid } from '../lib/peaks'
import type { Spectrogram } from '../lib/spectrogram'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

/** What the waveform view draws instead of the file's own audio while a preview is on. */
export interface PreviewDisplay {
  docId: number
  channels: Channels
  peaks: PeakPyramid
  /** The newest one built, which may lag `channels` by a render while settings are dragged. */
  spectrogram: Spectrogram | null
  spectrogramFor: Channels | null
}

/**
 * Keeps the waveform and spectrogram of the previewed audio, so a cleanup step can be seen as
 * well as heard before it is applied. Null when nothing is previewed or the preview is bypassed.
 */
export function usePreviewDisplay(
  doc: Ref<EditorDocument | null>,
  views: Ref<{ waveform: boolean; spectral: boolean }>,
) {
  const workspace = useAudioWorkspace()
  const display = shallowRef<PreviewDisplay | null>(null)
  let building = false

  /** One spectrogram job at a time; when it lands, build again if the audio moved on meanwhile. */
  async function buildSpectrograms(sampleRate: number) {
    if (building) return
    building = true
    try {
      for (;;) {
        const target = display.value
        if (!target || !views.value.spectral || target.spectrogramFor === target.channels) break
        const spectrogram = await runAnalysisJob({
          kind: 'spectrogram',
          channels: target.channels,
          sampleRate,
        })
        // Even a slightly stale one is closer to what is heard than the file's own.
        if (display.value?.docId === target.docId)
          display.value = markRaw({
            ...display.value,
            spectrogram: markRaw(spectrogram),
            spectrogramFor: target.channels,
          })
      }
    } catch {
      // The view keeps the last spectrogram it had.
    } finally {
      building = false
    }
  }

  watchThrottled(
    () =>
      [
        workspace.preview,
        workspace.previewBypass,
        doc.value?.channels,
        doc.value?.selection?.start,
        doc.value?.selection?.end,
        views.value.spectral,
      ] as const,
    () => {
      const current = doc.value
      if (!current?.channels || !workspace.preview || workspace.previewBypass) {
        display.value = null
        return
      }
      const channels = workspace.playbackChannels(current, current.channels)
      if (channels === current.channels) {
        display.value = null
        return
      }
      if (display.value?.channels !== channels) {
        // Another file's spectrogram would not line up; this file's last one is close enough.
        const previous = display.value?.docId === current.id ? display.value : null
        display.value = markRaw({
          docId: current.id,
          channels,
          peaks: markRaw(buildPeaks(channels)),
          spectrogram: previous?.spectrogram ?? null,
          spectrogramFor: previous?.spectrogramFor ?? null,
        })
      }
      void buildSpectrograms(current.sampleRate)
    },
    { throttle: 60, trailing: true, immediate: true },
  )

  return display
}
