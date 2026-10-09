import { watchThrottled } from '@vueuse/core'
import { markRaw, shallowRef, type Ref } from 'vue'
import type { Channels } from '../lib/operations'
import { buildPeaks, type PeakPyramid } from '../lib/peaks'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

/** What the waveform view draws instead of the file's own audio while a preview is on. */
export interface PreviewDisplay {
  docId: number
  channels: Channels
  peaks: PeakPyramid
}

/**
 * Keeps the waveform of the previewed audio, so a cleanup step can be seen as well as heard before
 * it is applied (the view builds its spectrogram). Null when nothing is previewed, the preview is
 * bypassed, or its render is not back from the worker yet.
 */
export function usePreviewDisplay(doc: Ref<EditorDocument | null>) {
  const workspace = useAudioWorkspace()
  const display = shallowRef<PreviewDisplay | null>(null)

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
      if (!current?.channels || !workspace.preview || workspace.previewBypass) {
        display.value = null
        return
      }
      const channels = workspace.playbackChannels(current, current.channels)
      if (channels === current.channels) {
        display.value = null
        return
      }
      if (display.value?.channels !== channels)
        display.value = markRaw({
          docId: current.id,
          channels,
          peaks: markRaw(buildPeaks(channels)),
        })
    },
    { throttle: 60, trailing: true, immediate: true },
  )

  return display
}
