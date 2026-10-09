<script setup lang="ts">
import { useLocalStorage } from '@vueuse/core'
import { FileArchive, FilePlus2, FolderDown, Loader2 } from 'lucide-vue-next'
import {
  DialogContent,
  DialogDescription,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { validateAudioFileName } from '@/features/tools/lib/audioChecks'
import { canOpenFolders } from '../lib/fileAccess'
import { formatTime } from '../lib/format'
import { exportNames, namePart, sortMarkers } from '../lib/markers'
import { useAudioWorkspace, type EditorDocument } from '../stores/workspace'

const props = defineProps<{ doc: EditorDocument | null }>()
const open = defineModel<boolean>({ required: true })

const workspace = useAudioWorkspace()
const settings = useLocalStorage(
  'vow.audioEditor.markerExport',
  { quest: '', character: '', first: 1, rename: false },
  { mergeDefaults: true },
)
const running = ref(false)

/** Range markers in time order: the order the files are numbered in. */
const ranges = computed(() =>
  sortMarkers((props.doc?.markers ?? []).filter((marker) => marker.length > 0)),
)
const pointCount = computed(() => (props.doc?.markers.length ?? 0) - ranges.value.length)
const first = computed(() => Math.max(1, Math.floor(Number(settings.value.first) || 1)))
const names = computed(() =>
  exportNames(ranges.value.length, settings.value.quest, settings.value.character, first.value),
)
const problem = computed(() => {
  if (!namePart(settings.value.quest)) return 'Enter the quest name.'
  if (!namePart(settings.value.character)) return 'Enter the character name.'
  if (ranges.value.length === 0) return 'There are no range markers to export.'
  return names.value[0] ? validateAudioFileName(names.value[0]) : null
})
const seconds = (frames: number) => frames / (props.doc?.sampleRate || 1)
const SHOWN = 200

async function run(target: 'zip' | 'folder' | 'open') {
  const doc = props.doc
  if (!doc || problem.value) return
  let directory: FileSystemDirectoryHandle | undefined
  if (target === 'folder') {
    try {
      // Asked first, while the click still counts as the person's gesture.
      directory = await window.showDirectoryPicker!({ mode: 'readwrite', id: 'vow-markers' })
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      throw error
    }
  }
  running.value = true
  try {
    const markers = ranges.value
    const fileNames = names.value
    const prefix = `${namePart(settings.value.quest)}-${namePart(settings.value.character)}`
    const count = await workspace.exportMarkers(doc, {
      markers,
      names: fileNames,
      target,
      directory,
      zipName: `${prefix}.zip`,
    })
    if (settings.value.rename) {
      const renamed = new Map(
        markers.map((marker, index) => [marker.id, fileNames[index]!.replace(/\.wav$/, '')]),
      )
      workspace.setMarkers(
        doc,
        'Name markers for export',
        doc.markers.map((marker) =>
          renamed.has(marker.id) ? { ...marker, name: renamed.get(marker.id)! } : marker,
        ),
      )
    }
    toast.success(
      target === 'open'
        ? `Opened ${count} line(s) as new files.`
        : target === 'folder'
          ? `Wrote ${count} file(s) to ${directory!.name}.`
          : `Exported ${count} file(s) to ${prefix}.zip.`,
    )
    open.value = false
  } catch (error) {
    toast.error(error instanceof Error ? error.message : 'The export failed.')
  } finally {
    running.value = false
  }
}

const field =
  'h-8 w-full rounded-md border bg-background px-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50'
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/50" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 flex max-h-[calc(100dvh-2rem)] w-[min(40rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-lg border bg-card p-5 text-card-foreground shadow-xl focus:outline-none"
        @interact-outside="(event) => running && event.preventDefault()"
        @escape-key-down="(event) => running && event.preventDefault()"
      >
        <div class="space-y-1">
          <DialogTitle class="text-base font-semibold">Export markers as lines</DialogTitle>
          <DialogDescription class="text-sm text-muted-foreground">
            Each range marker becomes its own WAV, named
            <span class="font-mono text-foreground">questname-charactername-number</span> and
            numbered in time order.
            <template v-if="pointCount">
              {{ pointCount }} point marker(s) have no length and are left out.
            </template>
          </DialogDescription>
        </div>

        <div class="grid gap-3 sm:grid-cols-[1fr_1fr_6rem]">
          <label class="space-y-1 text-xs font-medium">
            Quest name
            <input
              v-model="settings.quest"
              :class="field"
              placeholder="e.g. Anathema"
              autocomplete="off"
            />
          </label>
          <label class="space-y-1 text-xs font-medium">
            Character name
            <input
              v-model="settings.character"
              :class="field"
              placeholder="e.g. Zhight"
              autocomplete="off"
            />
          </label>
          <label class="space-y-1 text-xs font-medium">
            Start at
            <input
              v-model.number="settings.first"
              type="number"
              min="1"
              step="1"
              :class="field"
              class="tabular-nums"
            />
          </label>
        </div>

        <div class="min-h-0 flex-1 overflow-auto rounded-md border">
          <table class="w-full text-xs tabular-nums">
            <thead class="sticky top-0 bg-muted text-left text-muted-foreground">
              <tr>
                <th class="px-2 py-1.5 font-medium">File</th>
                <th class="px-2 py-1.5 font-medium">Marker</th>
                <th class="px-2 py-1.5 text-right font-medium">Start</th>
                <th class="px-2 py-1.5 text-right font-medium">Length</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(marker, index) in ranges.slice(0, SHOWN)"
                :key="marker.id"
                class="border-t"
              >
                <td class="whitespace-nowrap px-2 py-1 font-mono">
                  {{ problem ? '—' : names[index] }}
                </td>
                <td
                  class="max-w-[14rem] truncate px-2 py-1 text-muted-foreground"
                  :title="marker.name"
                >
                  {{ marker.name }}
                </td>
                <td class="px-2 py-1 text-right">{{ formatTime(seconds(marker.start)) }}</td>
                <td class="px-2 py-1 text-right">{{ formatTime(seconds(marker.length)) }}</td>
              </tr>
              <tr v-if="ranges.length > SHOWN" class="border-t">
                <td colspan="4" class="px-2 py-1 text-muted-foreground">
                  and {{ ranges.length - SHOWN }} more, up to
                  <span class="font-mono">{{ names[names.length - 1] }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <label class="flex items-center gap-2 text-xs">
          <input v-model="settings.rename" type="checkbox" class="size-3.5 accent-primary" />
          Also rename the markers to their file names (undo with Ctrl+Z)
        </label>

        <p v-if="problem" class="text-xs text-destructive">{{ problem }}</p>
        <p
          v-else-if="workspace.busy && running"
          class="flex items-center gap-2 text-xs text-muted-foreground"
          role="status"
        >
          <Loader2 class="size-3 animate-spin" /> {{ workspace.busy }}
        </p>

        <div class="flex flex-wrap justify-end gap-2">
          <Button variant="ghost" size="sm" :disabled="running" @click="open = false"
            >Cancel</Button
          >
          <Button
            variant="outline"
            size="sm"
            :disabled="running || !!problem"
            title="Add the lines to the file list as unsaved files, to clean up and level before saving"
            @click="run('open')"
          >
            <FilePlus2 /> Open in editor
          </Button>
          <Button
            v-if="canOpenFolders()"
            variant="outline"
            size="sm"
            :disabled="running || !!problem"
            title="Write the files into a folder; files with the same name are replaced"
            @click="run('folder')"
          >
            <FolderDown /> Save to folder
          </Button>
          <Button size="sm" :disabled="running || !!problem" @click="run('zip')">
            <Loader2 v-if="running" class="animate-spin" /><FileArchive v-else /> Download ZIP
          </Button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
