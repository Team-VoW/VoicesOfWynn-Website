<script setup lang="ts">
import { Search } from 'lucide-vue-next'
import { computed, nextTick, watch } from 'vue'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'
import FileListRow from './FileListRow.vue'

const emit = defineEmits<{ close: [docs: EditorDocument[]] }>()

const workspace = useAudioWorkspace()

const allChecked = computed({
  get: () =>
    workspace.visibleDocuments.length > 0 && workspace.visibleDocuments.every((doc) => doc.checked),
  set: (value: boolean) => {
    for (const doc of workspace.visibleDocuments) doc.checked = value
  },
})

/**
 * With focus in the list, Ctrl+A ticks every listed file and Delete closes the ticked ones (or the
 * open one when none are ticked). Elsewhere those keys keep editing the audio.
 */
function onKeydown(event: KeyboardEvent) {
  const key = event.key.toLowerCase()
  if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey && key === 'a') {
    event.preventDefault()
    allChecked.value = true
  } else if ((key === 'delete' || key === 'backspace') && !event.ctrlKey && !event.metaKey) {
    event.preventDefault()
    const ticked = workspace.visibleDocuments.filter((doc) => doc.checked)
    const targets =
      ticked.length > 0 ? ticked : workspace.activeDocument ? [workspace.activeDocument] : []
    if (targets.length > 0) emit('close', targets)
  }
}

// Arrow-key stepping can move past the visible rows; keep the active one in view.
watch(
  () => workspace.activeId,
  async (id) => {
    await nextTick()
    document.getElementById(`audio-file-${id}`)?.scrollIntoView({ block: 'nearest' })
  },
)
</script>

<template>
  <div class="flex min-h-0 flex-col rounded-md border bg-card">
    <div class="space-y-2 border-b p-2">
      <label class="relative block">
        <span class="sr-only">Filter files</span>
        <Search
          class="pointer-events-none absolute left-2 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground"
        />
        <input
          v-model="workspace.search"
          type="search"
          placeholder="Filter files"
          class="h-8 w-full rounded-md border bg-background pl-7 pr-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
        />
      </label>
      <div class="flex items-center justify-between px-1 text-xs text-muted-foreground">
        <label class="inline-flex items-center gap-1.5">
          <input v-model="allChecked" type="checkbox" class="size-3.5 accent-primary" />
          Tick all
        </label>
        <span>
          {{ workspace.documents.length }} open<template v-if="workspace.dirtyDocuments.length">
            · {{ workspace.dirtyDocuments.length }} unsaved</template
          >
        </span>
      </div>
    </div>

    <ul
      class="relative min-h-0 flex-1 overflow-y-auto p-1 outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
      role="listbox"
      aria-label="Open files"
      aria-multiselectable="true"
      tabindex="0"
      @keydown="onKeydown"
    >
      <FileListRow
        v-for="doc in workspace.visibleDocuments"
        :key="doc.id"
        :doc="doc"
        :active="doc.id === workspace.activeId"
        @select="workspace.setActive(doc.id)"
        @check="(checked) => (doc.checked = checked)"
        @close="emit('close', [doc])"
      />
    </ul>
  </div>
</template>
