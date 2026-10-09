import { useEventListener } from '@vueuse/core'
import type { ZeroCrossingAdjustment } from '../lib/editing'
import type { SpectralTool } from '../stores/workspace'

export interface EditorShortcutHandlers {
  togglePlay: () => void
  toggleLoop: () => void
  selectAll: () => void
  deleteSelection: () => void
  cut: () => void
  copy: () => void
  paste: () => void
  mixPaste: () => void
  copyToNew: () => void
  zeroCrossings: (adjustment: ZeroCrossingAdjustment) => void
  undo: () => void
  redo: () => void
  save: () => void
  saveAs: () => void
  previousFile: () => void
  nextFile: () => void
  zoomIn: () => void
  zoomOut: () => void
  zoomToFit: () => void
  toStart: () => void
  toEnd: () => void
  clearSelection: () => void
  toggleBypass: () => void
  addMarker: () => void
  spectralTool: (tool: SpectralTool) => void
  /** The tool in use, to go back to when a tool key that was held down is let go. */
  currentSpectralTool: () => SpectralTool
  /** Heals the spectral selection. */
  heal: () => void
}

export const SHORTCUT_HELP: [keys: string, action: string][] = [
  ['Space', 'Play / stop'],
  ['Shift+Space', 'Loop selection'],
  ['Ctrl+A', 'Select all'],
  ['Del', 'Delete selection'],
  ['Ctrl+X / C / V', 'Cut / copy / paste'],
  ['Ctrl+Shift+V', 'Mix paste'],
  ['Ctrl+Alt+C', 'Copy to new file'],
  ['Shift+I / Shift+O', 'Zero crossings inward / outward'],
  ['Shift+H / J', 'Start to zero crossing left / right'],
  ['Shift+K / L', 'End to zero crossing left / right'],
  ['Ctrl+Z / Ctrl+Shift+Z', 'Undo / redo'],
  ['Ctrl+S', 'Save over original'],
  ['Ctrl+Shift+S', 'Save as'],
  ['↑ / ↓', 'Previous / next file'],
  ['+ / − / 0', 'Zoom in / out / fit'],
  ['Home / End', 'Cursor to start / end'],
  ['Esc', 'Clear selection'],
  ['B', 'Preview: original / processed'],
  ['Ctrl+B or M', 'Add marker (range over the selection)'],
  ['T / E / D / P / F', 'Time / marquee / lasso / brush / frequency tool'],
  ['Hold a tool key', 'Use that tool until you let go'],
  ['H', 'Spot healing brush'],
  ['Del on a spectral selection', 'Silence the selected area'],
  ['Ctrl+U', 'Heal the spectral selection'],
  ['Alt+wheel on spectrogram', 'Zoom frequencies (Shift scrolls)'],
  ['Ctrl+A in file list', 'Tick all files'],
  ['Del in file list', 'Close ticked files'],
]

function isTyping(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) return false
  if (target.isContentEditable) return true
  if (target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) return true
  if (target instanceof HTMLInputElement)
    return !['checkbox', 'radio', 'button', 'range'].includes(target.type)
  return false
}

const SPECTRAL_TOOL_KEYS: Record<string, SpectralTool> = {
  t: 'time',
  e: 'marquee',
  d: 'lasso',
  p: 'brush',
  f: 'frequency',
  h: 'heal',
}

const ZERO_CROSSING_KEYS: Record<string, ZeroCrossingAdjustment> = {
  i: 'inward',
  o: 'outward',
  h: 'startLeft',
  j: 'startRight',
  k: 'endLeft',
  l: 'endRight',
}

/** Leaves Ctrl+C / Ctrl+X to the browser while the person has text on the page highlighted. */
function hasTextSelection() {
  return (window.getSelection?.()?.toString() ?? '') !== ''
}

/** A tool key held this long switches back when let go, like Photoshop's spring-loaded tools. */
const SPRING_MS = 300

export function useEditorShortcuts(handlers: EditorShortcutHandlers) {
  /** The tool key down now, and the tool to go back to if it turns out to be held. */
  let spring: { key: string; previous: SpectralTool; at: number } | null = null

  useEventListener(window, 'keyup', (event: KeyboardEvent) => {
    if (!spring || event.key.toLowerCase() !== spring.key) return
    if (performance.now() - spring.at >= SPRING_MS) handlers.spectralTool(spring.previous)
    spring = null
  })
  // Switching windows mid-hold never delivers the keyup; keep the tool rather than guess.
  useEventListener(window, 'blur', () => (spring = null))

  useEventListener(window, 'keydown', (event: KeyboardEvent) => {
    if (event.defaultPrevented || isTyping(event.target)) return
    // Keys in a dialog (Space on its buttons, …) belong to the dialog.
    if (event.target instanceof Element && event.target.closest('[role="dialog"]')) return
    const mod = event.ctrlKey || event.metaKey
    const key = event.key.toLowerCase()
    let action: (() => void) | null = null

    if (mod && event.altKey && event.code === 'KeyC') action = handlers.copyToNew
    else if (mod && key === 'c' && !hasTextSelection()) action = handlers.copy
    else if (mod && key === 'x' && !hasTextSelection()) action = handlers.cut
    else if (mod && key === 'v') action = event.shiftKey ? handlers.mixPaste : handlers.paste
    else if (mod && key === 's') action = event.shiftKey ? handlers.saveAs : handlers.save
    else if (mod && key === 'z') action = event.shiftKey ? handlers.redo : handlers.undo
    else if (mod && key === 'y') action = handlers.redo
    else if (mod && key === 'a') action = handlers.selectAll
    else if (mod && key === 'b') action = handlers.addMarker
    else if (mod && key === 'u') action = handlers.heal
    else if (mod || event.altKey) return
    else if (event.shiftKey && ZERO_CROSSING_KEYS[key]) {
      const adjustment = ZERO_CROSSING_KEYS[key]
      action = () => handlers.zeroCrossings(adjustment)
    } else if (key === ' ') action = event.shiftKey ? handlers.toggleLoop : handlers.togglePlay
    else if (key === 'delete' || key === 'backspace') action = handlers.deleteSelection
    else if (key === 'arrowup') action = handlers.previousFile
    else if (key === 'arrowdown') action = handlers.nextFile
    else if (key === '+' || key === '=') action = handlers.zoomIn
    else if (key === '-' || key === '_') action = handlers.zoomOut
    else if (key === '0') action = handlers.zoomToFit
    else if (key === 'home') action = handlers.toStart
    else if (key === 'end') action = handlers.toEnd
    else if (key === 'escape') action = handlers.clearSelection
    else if (key === 'b') action = handlers.toggleBypass
    else if (key === 'm') action = handlers.addMarker
    else if (!event.shiftKey && SPECTRAL_TOOL_KEYS[key]) {
      const tool = SPECTRAL_TOOL_KEYS[key]
      action = () => {
        // Held keys repeat; only the first press switches and remembers where it came from.
        if (event.repeat) return
        spring = { key, previous: handlers.currentSpectralTool(), at: performance.now() }
        handlers.spectralTool(tool)
      }
    }

    if (!action) return
    event.preventDefault()
    action()
  })
}
