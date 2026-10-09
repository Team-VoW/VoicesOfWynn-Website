<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'
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
import { isDirty, useAudioWorkspace, type EditorDocument } from '../stores/workspace'

/** The files being closed; the dialog only opens when some of them have unsaved changes. */
const docs = defineModel<EditorDocument[] | null>({ required: true })

const workspace = useAudioWorkspace()
const saving = ref(false)
const SHOWN_NAMES = 6

const dirty = computed(() => (docs.value ?? []).filter(isDirty))
const open = computed({
  get: () => dirty.value.length > 0,
  set: (value) => {
    if (!value && !saving.value) docs.value = null
  },
})

function closeAll() {
  if (docs.value) workspace.closeMany(docs.value)
  docs.value = null
}

async function saveAndClose() {
  saving.value = true
  try {
    if (dirty.value.length === 1) {
      // One file goes through the normal save, so it can still ask where to put it.
      const saved = await workspace.save(dirty.value[0]!)
      if (!saved) return
    } else {
      const { zipped } = await workspace.saveAll(dirty.value)
      if (zipped)
        toast.info(`${zipped} file(s) without a writable original went into a ZIP download.`)
    }
    closeAll()
  } catch (error) {
    toast.error(error instanceof Error ? error.message : 'Saving failed; nothing was closed.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/50" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 space-y-4 rounded-lg border bg-card p-5 text-card-foreground shadow-xl focus:outline-none"
      >
        <div class="space-y-1">
          <DialogTitle class="text-base font-semibold">
            <template v-if="dirty.length === 1">Save changes to {{ dirty[0]!.name }}?</template>
            <template v-else>Save changes to {{ dirty.length }} files?</template>
          </DialogTitle>
          <DialogDescription class="text-sm text-muted-foreground">
            <template v-if="docs && docs.length > dirty.length">
              Closing {{ docs.length }} files; {{ dirty.length }} have unsaved changes.
            </template>
            <template v-else>Discarded changes cannot be undone.</template>
          </DialogDescription>
        </div>
        <ul v-if="dirty.length > 1" class="space-y-0.5 text-sm">
          <li v-for="entry in dirty.slice(0, SHOWN_NAMES)" :key="entry.id" class="truncate">
            {{ entry.name }}
          </li>
          <li v-if="dirty.length > SHOWN_NAMES" class="text-muted-foreground">
            and {{ dirty.length - SHOWN_NAMES }} more
          </li>
        </ul>
        <div class="flex justify-end gap-2">
          <Button variant="ghost" size="sm" :disabled="saving" @click="docs = null">Cancel</Button>
          <Button variant="outline" size="sm" :disabled="saving" @click="closeAll">
            Discard changes
          </Button>
          <Button size="sm" :disabled="saving" @click="saveAndClose">
            <Loader2 v-if="saving" class="animate-spin" />
            {{ dirty.length === 1 ? 'Save' : 'Save all' }}
          </Button>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
