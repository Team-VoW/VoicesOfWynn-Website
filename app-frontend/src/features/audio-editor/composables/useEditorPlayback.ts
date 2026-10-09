import { onBeforeUnmount, ref } from 'vue'
import { useAudioPlayback, type PlaybackOwner } from '@/components/audio/useAudioPlayback'
import type { Channels } from '../lib/operations'
import type { EditorDocument } from '../stores/workspace'

/** How long `swap` crossfades between the old and new audio, so switching never clicks. */
const SWAP_FADE_SECONDS = 0.015

/**
 * Plays the in-memory buffers of the active document, so edits are heard before they are saved.
 * Stopping leaves the cursor where playback stopped, so playing again resumes from there.
 */
export function useEditorPlayback() {
  const playing = ref(false)
  const looping = ref(false)
  /** Playhead in frames while playing, otherwise null. */
  const position = ref<number | null>(null)
  const { claim, release } = useAudioPlayback()

  let context: AudioContext | null = null
  let source: AudioBufferSourceNode | null = null
  let sourceLevel: GainNode | null = null
  let frame = 0
  let startedAt = 0
  let range = { start: 0, end: 0 }
  let sampleRate = 48000
  let playingDoc: EditorDocument | null = null
  /** Bumped by every stop, so a play still waiting for the context to resume knows it was superseded. */
  let playToken = 0
  const buffers = new WeakMap<Channels, AudioBuffer>()

  const owner: PlaybackOwner = {
    pause: () => stop(),
    currentTime: 0,
  }

  function bufferFor(channels: Channels, rate: number) {
    let buffer = buffers.get(channels)
    if (!buffer) {
      buffer = new AudioBuffer({
        numberOfChannels: channels.length,
        length: Math.max(1, channels[0]?.length ?? 1),
        sampleRate: rate,
      })
      channels.forEach((channel, index) =>
        buffer!.copyToChannel(channel as Float32Array<ArrayBuffer>, index),
      )
      buffers.set(channels, buffer)
    }
    return buffer
  }

  function currentFrame(audio: AudioContext) {
    const elapsed = (audio.currentTime - startedAt) * sampleRate
    const length = range.end - range.start
    return looping.value && length > 0
      ? range.start + (elapsed % length)
      : Math.min(range.end, range.start + elapsed)
  }

  function tick() {
    if (!context || !playing.value) return
    position.value = currentFrame(context)
    frame = requestAnimationFrame(tick)
  }

  function stop() {
    playToken++
    if (playing.value && context && playingDoc) {
      playingDoc.cursor = Math.min(Math.round(currentFrame(context)), playingDoc.frames)
    }
    playingDoc = null
    if (source) {
      source.onended = null
      try {
        source.stop()
      } catch {
        // Already stopped.
      }
      source.disconnect()
      source = null
    }
    sourceLevel?.disconnect()
    sourceLevel = null
    cancelAnimationFrame(frame)
    playing.value = false
    position.value = null
    release(owner)
  }

  /** Plays the selection when there is one, otherwise from the cursor to the end. */
  async function play(doc: EditorDocument, channels: Channels, loop = false) {
    stop()
    const token = playToken
    context ??= new AudioContext()
    if (context.state === 'suspended') await context.resume()
    if (token !== playToken) return
    claim(owner)

    sampleRate = doc.sampleRate
    const frames = channels[0]?.length ?? 0
    const selection = doc.selection
    if (selection && selection.end > selection.start) {
      const fromCursor = doc.cursor > selection.start && doc.cursor < selection.end && !loop
      range = { start: fromCursor ? doc.cursor : selection.start, end: selection.end }
    } else {
      range = { start: doc.cursor >= frames ? 0 : doc.cursor, end: frames }
    }
    if (range.end <= range.start) return

    looping.value = loop
    startSource(context, bufferFor(channels, doc.sampleRate), range.start, false)
    playingDoc = doc
    playing.value = true
    frame = requestAnimationFrame(tick)
  }

  /** Starts `buffer` at frame `from` of the current range, optionally fading it in. */
  function startSource(audio: AudioContext, buffer: AudioBuffer, from: number, fadeIn: boolean) {
    const node = audio.createBufferSource()
    const level = audio.createGain()
    node.buffer = buffer
    node.connect(level).connect(audio.destination)
    if (fadeIn) {
      level.gain.setValueAtTime(0, audio.currentTime)
      level.gain.linearRampToValueAtTime(1, audio.currentTime + SWAP_FADE_SECONDS)
    }
    if (looping.value) {
      node.loop = true
      node.loopStart = range.start / sampleRate
      node.loopEnd = range.end / sampleRate
      node.start(0, from / sampleRate)
    } else {
      node.start(0, from / sampleRate, (range.end - from) / sampleRate)
    }
    node.onended = () => {
      if (source === node) stop()
    }
    source = node
    sourceLevel = level
    startedAt = audio.currentTime - (from - range.start) / sampleRate
  }

  /**
   * Carries on playing from the same spot with other audio of the same length, crossfading so
   * the switch is seamless. Used to A/B a preview and to follow an edit applied mid-playback.
   */
  function swap(channels: Channels) {
    const audio = context
    const old = source
    const oldLevel = sourceLevel
    if (!playing.value || !audio || !old || !oldLevel) return
    if ((channels[0]?.length ?? 0) < range.end) return
    const buffer = bufferFor(channels, sampleRate)
    if (old.buffer === buffer) return
    const from = Math.floor(currentFrame(audio))
    if (from >= range.end) return

    const now = audio.currentTime
    oldLevel.gain.setValueAtTime(1, now)
    oldLevel.gain.linearRampToValueAtTime(0, now + SWAP_FADE_SECONDS)
    old.onended = () => {
      old.disconnect()
      oldLevel.disconnect()
    }
    old.stop(now + SWAP_FADE_SECONDS)
    startSource(audio, buffer, from, true)
  }

  async function toggle(doc: EditorDocument, channels: Channels, loop = false) {
    if (playing.value) stop()
    else await play(doc, channels, loop)
  }

  onBeforeUnmount(() => {
    stop()
    void context?.close()
    context = null
  })

  return { playing, looping, position, play, stop, toggle, swap }
}
