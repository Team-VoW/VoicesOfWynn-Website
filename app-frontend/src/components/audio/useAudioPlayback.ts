import { ref } from 'vue'

/**
 * Only one recording plays at a time across the whole page, so claiming playback stops whoever
 * held it. Module scope rather than a per-component ref: the players are siblings that never
 * share a parent, and the legacy site achieved the same thing with a document-level listener.
 */
/** An <audio> element, or anything else that plays (the audio editor's Web Audio source). */
export interface PlaybackOwner {
  pause(): void
  currentTime: number
}

const current = ref<PlaybackOwner | null>(null)

export function useAudioPlayback() {
  function claim(element: PlaybackOwner) {
    if (current.value && current.value !== element) {
      current.value.pause()
      current.value.currentTime = 0
    }
    current.value = element
  }

  function release(element: PlaybackOwner) {
    if (current.value === element) current.value = null
  }

  return { claim, release }
}
