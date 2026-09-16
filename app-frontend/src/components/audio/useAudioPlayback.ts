import { ref } from 'vue'

/**
 * Only one recording plays at a time across the whole page, so claiming playback stops whoever
 * held it. Module scope rather than a per-component ref: the players are siblings that never
 * share a parent, and the legacy site achieved the same thing with a document-level listener.
 */
const current = ref<HTMLAudioElement | null>(null)

export function useAudioPlayback() {
  function claim(element: HTMLAudioElement) {
    if (current.value && current.value !== element) {
      current.value.pause()
      current.value.currentTime = 0
    }
    current.value = element
  }

  function release(element: HTMLAudioElement) {
    if (current.value === element) current.value = null
  }

  return { claim, release }
}
