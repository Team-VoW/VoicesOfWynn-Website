<script setup lang="ts">
import { useLocalStorage } from '@vueuse/core'
import { ChevronDown, Download, Flag, Plus, Search, X } from 'lucide-vue-next'
import { computed, nextTick, ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { formatTime } from '../lib/format'
import type { Marker } from '../lib/markers'
import MarkerMenu from './MarkerMenu.vue'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

const props = defineProps<{ doc: EditorDocument | null }>()
const emit = defineEmits<{ reveal: [marker: Marker]; export: [] }>()

const workspace = useAudioWorkspace()
const open = useLocalStorage('vow.audioEditor.markersOpen', true)
const filter = ref('')
const editingId = ref<number | null>(null)
const draft = ref('')
const list = ref<HTMLUListElement | null>(null)

const markers = computed(() => props.doc?.markers ?? [])
const rangeCount = computed(() => markers.value.filter((marker) => marker.length > 0).length)
const shown = computed(() => {
  const query = filter.value.trim().toLowerCase()
  return query
    ? markers.value.filter(
        (marker) =>
          marker.name.toLowerCase().includes(query) || marker.comment.toLowerCase().includes(query),
      )
    : markers.value
})

const seconds = (frames: number) => frames / (props.doc?.sampleRate || 1)

function pick(marker: Marker) {
  if (!props.doc) return
  workspace.selectMarker(props.doc, marker.id)
  emit('reveal', marker)
}

function add() {
  if (!props.doc || props.doc.status !== 'ready') return
  open.value = true
  workspace.addMarker(props.doc)
}

async function startRename(marker: Marker) {
  open.value = true
  // A filter that hides it would leave nothing to type into.
  if (!shown.value.some((entry) => entry.id === marker.id)) filter.value = ''
  editingId.value = marker.id
  draft.value = marker.name
  await nextTick()
  const input = document.getElementById(`marker-name-${marker.id}`) as HTMLInputElement | null
  input?.focus()
  input?.select()
}

function finishRename(commit: boolean) {
  const id = editingId.value
  editingId.value = null
  if (!commit || id === null || !props.doc) return
  const marker = props.doc.markers.find((entry) => entry.id === id)
  if (marker && marker.name !== draft.value)
    workspace.updateMarker(props.doc, id, { name: draft.value }, 'Rename marker')
  list.value?.focus()
}

function remove(marker: Marker) {
  if (props.doc) workspace.removeMarkers(props.doc, [marker.id])
}

/** Arrows walk the list, Enter or F2 renames, Delete removes; the editor's own keys stay out. */
function onKeydown(event: KeyboardEvent) {
  const doc = props.doc
  if (!doc || editingId.value !== null) return
  const index = shown.value.findIndex((marker) => marker.id === doc.selectedMarkerId)
  const current = shown.value[index]
  let handled = true
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    const next =
      shown.value[
        Math.max(0, Math.min(shown.value.length - 1, index + (event.key === 'ArrowDown' ? 1 : -1)))
      ]
    if (next) pick(next)
  } else if ((event.key === 'Enter' || event.key === 'F2') && current) {
    void startRename(current)
  } else if ((event.key === 'Delete' || event.key === 'Backspace') && current) {
    const after = shown.value[index + 1] ?? shown.value[index - 1]
    remove(current)
    if (after) pick(after)
  } else {
    handled = false
  }
  if (handled) {
    event.preventDefault()
    event.stopPropagation()
  }
}

defineExpose({ startRename })

const menuTarget = ref<{ marker: Marker | null; frame: number | null }>({
  marker: null,
  frame: null,
})

function onContextMenu(event: MouseEvent) {
  const row = (event.target as Element).closest<HTMLElement>('[data-marker-id]')
  const id = Number(row?.dataset.markerId)
  const marker = props.doc?.markers.find((entry) => entry.id === id) ?? null
  menuTarget.value = { marker, frame: null }
  if (marker && props.doc) workspace.highlightMarker(props.doc, marker.id)
}

// Keep the picked marker in view, whether picked here, on the ruler or by adding one.
watch(
  () => props.doc?.selectedMarkerId,
  async (id) => {
    if (id == null) return
    await nextTick()
    document.getElementById(`marker-row-${id}`)?.scrollIntoView({ block: 'nearest' })
  },
)
</script>

<template>
  <section
    class="flex min-h-0 flex-col rounded-md border bg-card"
    :class="open ? 'flex-1' : ''"
    aria-label="Markers"
  >
    <div class="flex items-center gap-1 px-2 py-1.5" :class="open ? 'border-b' : ''">
      <button
        type="button"
        class="flex min-w-0 flex-1 items-center gap-1.5 rounded px-1 py-0.5 text-left text-sm font-medium hover:bg-accent"
        :aria-expanded="open"
        @click="open = !open"
      >
        <ChevronDown
          class="size-3.5 shrink-0 transition-transform"
          :class="open ? '' : '-rotate-90'"
        />
        Markers
        <span class="text-xs font-normal tabular-nums text-muted-foreground">
          {{ markers.length }}
        </span>
      </button>
      <Button
        size="icon-sm"
        variant="ghost"
        :disabled="!doc || doc.status !== 'ready'"
        aria-label="Add marker (Ctrl+B)"
        title="Add marker (Ctrl+B): a range over the selection, or a point at the cursor"
        @click="add"
      >
        <Plus />
      </Button>
      <Button
        size="icon-sm"
        variant="ghost"
        :disabled="!rangeCount"
        aria-label="Export ranges as files"
        title="Export each range marker as its own file"
        @click="emit('export')"
      >
        <Download />
      </Button>
    </div>

    <template v-if="open">
      <label v-if="markers.length > 8" class="relative mx-2 mt-2 block">
        <span class="sr-only">Filter markers</span>
        <Search
          class="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground"
        />
        <input
          v-model="filter"
          type="search"
          placeholder="Filter markers"
          class="h-7 w-full rounded-md border bg-background pl-7 pr-2 text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
        />
      </label>

      <p v-if="markers.length === 0" class="px-3 py-3 text-xs text-muted-foreground">
        <Flag class="mb-1 size-4 text-primary" />
        Select a line and press <kbd class="rounded border px-1 font-mono">Ctrl+B</kbd> to mark it.
        Markers are saved into the WAV, where Audition reads them too.
      </p>

      <MarkerMenu v-else :doc="doc" :target="menuTarget">
        <ul
          ref="list"
          class="min-h-0 flex-1 overflow-y-auto p-1 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
          role="listbox"
          aria-label="Markers"
          tabindex="0"
          @keydown="onKeydown"
          @contextmenu="onContextMenu"
        >
          <li
            v-for="marker in shown"
            :id="`marker-row-${marker.id}`"
            :key="marker.id"
            :data-marker-id="marker.id"
            role="option"
            :aria-selected="marker.id === doc?.selectedMarkerId"
            class="group flex cursor-pointer items-start gap-2 rounded px-2 py-1"
            :class="
              marker.id === doc?.selectedMarkerId
                ? 'bg-primary/10 ring-1 ring-primary/40'
                : 'hover:bg-accent'
            "
            :title="marker.comment || undefined"
            @click="pick(marker)"
            @dblclick="startRename(marker)"
          >
            <span
              class="mt-1 h-3 w-1 shrink-0 rounded-full"
              :class="marker.length > 0 ? 'bg-sky-500' : 'bg-sky-300'"
              aria-hidden="true"
            />
            <div class="min-w-0 flex-1">
              <input
                v-if="editingId === marker.id"
                :id="`marker-name-${marker.id}`"
                v-model="draft"
                class="h-6 w-full rounded border bg-background px-1 text-xs outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
                aria-label="Marker name"
                @click.stop
                @keydown.enter.prevent="finishRename(true)"
                @keydown.esc.prevent.stop="finishRename(false)"
                @blur="finishRename(true)"
              />
              <p v-else class="truncate text-xs font-medium">
                {{ marker.name || 'Untitled marker' }}
              </p>
              <p class="truncate text-[11px] tabular-nums text-muted-foreground">
                {{ formatTime(seconds(marker.start)) }}
                <template v-if="marker.length > 0">
                  · {{ formatTime(seconds(marker.length)) }}
                </template>
                <template v-else> · point</template>
              </p>
            </div>
            <button
              type="button"
              class="mt-0.5 rounded p-0.5 text-muted-foreground opacity-0 hover:bg-background hover:text-foreground focus-visible:opacity-100 group-hover:opacity-100"
              :aria-label="`Delete ${marker.name || 'marker'}`"
              title="Delete marker"
              @click.stop="remove(marker)"
            >
              <X class="size-3.5" />
            </button>
          </li>
          <li v-if="shown.length === 0" class="px-2 py-2 text-xs text-muted-foreground">
            No markers match.
          </li>
        </ul>
      </MarkerMenu>
    </template>
  </section>
</template>
