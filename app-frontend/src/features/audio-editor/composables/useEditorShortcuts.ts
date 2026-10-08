import { useEventListener } from '@vueuse/core'
import type { ZeroCrossingAdjustment } from '../lib/editing'

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

export function useEditorShortcuts(handlers: EditorShortcutHandlers) {
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

    if (!action) return
    event.preventDefault()
    action()
  })
}
