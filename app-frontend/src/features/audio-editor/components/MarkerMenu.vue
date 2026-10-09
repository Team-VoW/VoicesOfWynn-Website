<script setup lang="ts">
import {
  BetweenHorizontalStart,
  Combine,
  Crosshair,
  Flag,
  MapPin,
  Play,
  Scissors,
  SlidersHorizontal,
  TextCursorInput,
  Trash2,
  ZoomIn,
} from 'lucide-vue-next'
import {
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuPortal,
  ContextMenuRoot,
  ContextMenuSeparator,
  ContextMenuTrigger,
} from 'reka-ui'
import { computed, inject } from 'vue'
import { MARKER_ACTIONS } from '../composables/markerActions'
import { formatTime } from '../lib/format'
import type { Marker } from '../lib/markers'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

/**
 * Right-click menu for markers, wrapped around whatever shows them. The wrapper says what was
 * clicked by setting `target` in its own `contextmenu` handler, which runs before this opens.
 * Focus moving away does not close it, only a click elsewhere or Escape: right after picking an
 * item, the closing menu hands focus back to its trigger, which would shut a menu opened that fast.
 */
const props = defineProps<{
  doc: EditorDocument | null
  /** The marker under the pointer, and the frame there (null outside the timeline). */
  target: { marker: Marker | null; frame: number | null }
}>()

const workspace = useAudioWorkspace()
const actions = inject(MARKER_ACTIONS)!

const marker = computed(() => props.target.marker)
const isRange = computed(() => !!marker.value && marker.value.length > 0)
const seconds = (frames: number) => formatTime(frames / (props.doc?.sampleRate || 1))
const span = computed(() => {
  const target = marker.value
  if (!target) return ''
  return target.length > 0
    ? `${seconds(target.start)} – ${seconds(target.start + target.length)}`
    : seconds(target.start)
})

/** The next marker in time, for Merge with next. */
const next = computed(() => {
  const doc = props.doc
  const target = marker.value
  if (!doc || !target) return null
  const index = doc.markers.findIndex((entry) => entry.id === target.id)
  return doc.markers[index + 1] ?? null
})

const selection = computed(() => props.doc?.selection ?? null)
const selectionDiffers = computed(() => {
  const target = marker.value
  const range = selection.value
  return (
    !!target &&
    !!range &&
    (range.start !== target.start || range.end - range.start !== target.length)
  )
})
const cursorInside = computed(() => {
  const target = marker.value
  const cursor = props.doc?.cursor ?? -1
  return !!target && cursor > target.start && cursor < target.start + target.length
})

function run(task: (doc: EditorDocument, target: Marker) => void) {
  const doc = props.doc
  const target = marker.value
  if (doc && target) task(doc, target)
}

function addHere() {
  const doc = props.doc
  const frame = props.target.frame
  if (!doc || doc.status !== 'ready') return
  const range = doc.selection
  // Inside the selection it marks the selection, as Ctrl+B does; elsewhere a point right there.
  if (range && frame !== null && frame >= range.start && frame <= range.end)
    workspace.addMarker(doc)
  else workspace.addMarker(doc, { start: frame ?? doc.cursor, length: 0 })
}

const item =
  'relative flex cursor-default select-none items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-none data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0 [&_svg]:text-muted-foreground'
const keys = 'ml-auto pl-4 text-xs tracking-wide text-muted-foreground'
</script>

<template>
  <ContextMenuRoot :modal="false">
    <ContextMenuTrigger as-child :disabled="!doc || doc.status !== 'ready'">
      <slot />
    </ContextMenuTrigger>
    <ContextMenuPortal>
      <ContextMenuContent
        class="z-50 w-[17rem] overflow-hidden rounded-md border bg-popover p-1 text-popover-foreground shadow-md data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
        :collision-padding="8"
        @focus-outside="(event: Event) => event.preventDefault()"
      >
        <template v-if="marker">
          <ContextMenuLabel class="max-w-[18rem] px-2 py-1.5">
            <p class="truncate text-sm font-medium">{{ marker.name || 'Untitled marker' }}</p>
            <p class="text-xs tabular-nums text-muted-foreground">
              {{ isRange ? 'Range' : 'Point' }} · {{ span }}
            </p>
          </ContextMenuLabel>
          <ContextMenuSeparator class="-mx-1 my-1 h-px bg-border" />
          <ContextMenuItem :class="item" @select="actions.play(marker)">
            <Play /> Play
          </ContextMenuItem>
          <ContextMenuItem
            :class="item"
            @select="run((doc, target) => workspace.selectMarker(doc, target.id))"
          >
            <Crosshair /> {{ isRange ? 'Select range' : 'Move cursor here' }}
          </ContextMenuItem>
          <ContextMenuItem v-if="isRange" :class="item" @select="actions.reveal(marker, true)">
            <ZoomIn /> Zoom to marker
          </ContextMenuItem>
          <ContextMenuSeparator class="-mx-1 my-1 h-px bg-border" />
          <ContextMenuItem :class="item" @select="actions.rename(marker)">
            <TextCursorInput /> Rename <span :class="keys">F2</span>
          </ContextMenuItem>
          <ContextMenuItem :class="item" @select="actions.edit(marker)">
            <SlidersHorizontal /> Properties…
          </ContextMenuItem>
          <ContextMenuItem
            v-if="selectionDiffers"
            :class="item"
            @select="
              run((doc, target) =>
                workspace.updateMarker(
                  doc,
                  target.id,
                  { start: selection!.start, length: selection!.end - selection!.start },
                  'Set marker to selection',
                ),
              )
            "
          >
            <BetweenHorizontalStart /> Set to selection
          </ContextMenuItem>
          <ContextMenuItem
            v-if="isRange"
            :class="item"
            @select="
              run((doc, target) =>
                workspace.updateMarker(doc, target.id, { length: 0 }, 'Convert to point marker'),
              )
            "
          >
            <MapPin /> Convert to point
          </ContextMenuItem>
          <ContextMenuItem
            v-if="isRange"
            :class="item"
            :disabled="!cursorInside"
            @select="run((doc, target) => workspace.splitMarker(doc, target.id, doc.cursor))"
          >
            <Scissors /> Split at cursor
          </ContextMenuItem>
          <ContextMenuItem
            v-if="next"
            :class="item"
            @select="run((doc, target) => workspace.mergeMarkers(doc, [target.id, next!.id]))"
          >
            <Combine />
            <span class="min-w-0 truncate">Merge with {{ next.name || 'next marker' }}</span>
          </ContextMenuItem>
          <ContextMenuSeparator class="-mx-1 my-1 h-px bg-border" />
          <ContextMenuItem
            :class="[
              item,
              'text-destructive data-[highlighted]:text-destructive [&_svg]:!text-destructive',
            ]"
            @select="run((doc, target) => workspace.removeMarkers(doc, [target.id]))"
          >
            <Trash2 /> Delete marker
          </ContextMenuItem>
        </template>
        <template v-else>
          <ContextMenuItem :class="item" @select="addHere">
            <Flag />
            {{
              selection &&
              target.frame !== null &&
              target.frame >= selection.start &&
              target.frame <= selection.end
                ? 'Add range marker over selection'
                : 'Add marker here'
            }}
            <span :class="keys">Ctrl+B</span>
          </ContextMenuItem>
        </template>
      </ContextMenuContent>
    </ContextMenuPortal>
  </ContextMenuRoot>
</template>
