<script setup lang="ts">
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Pencil, Plus, Trash2, X } from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Broadcast } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useBroadcasts, useDeleteBroadcast, useSaveBroadcast } from '../queries'

const { data, isPending: isLoading } = useBroadcasts()
const save = useSaveBroadcast()
const remove = useDeleteBroadcast()

const editingId = ref<number | null>(null)
const isEditorOpen = ref(false)
const error = ref('')
const form = ref({ content: '', activeFrom: '', activeUntil: '' })

const isSaving = computed(() => save.isPending.value)
const broadcasts = computed(() => data.value?.broadcasts ?? [])

/** `datetime-local` has no timezone, and the API stores UTC — convert on both edges. */
function toLocalInput(iso: string) {
  const date = new Date(iso)
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

function toIso(local: string) {
  return local ? new Date(local).toISOString() : ''
}

function isLive(broadcast: Broadcast) {
  const now = Date.now()
  return new Date(broadcast.activeFrom).getTime() <= now && new Date(broadcast.activeUntil).getTime() >= now
}

function startCreate() {
  const now = new Date()
  const inAWeek = new Date(now.getTime() + 7 * 24 * 3600 * 1000)
  editingId.value = null
  form.value = {
    content: '',
    activeFrom: toLocalInput(now.toISOString()),
    activeUntil: toLocalInput(inAWeek.toISOString()),
  }
  error.value = ''
  isEditorOpen.value = true
}

function startEdit(broadcast: Broadcast) {
  editingId.value = broadcast.id
  form.value = {
    content: broadcast.content,
    activeFrom: toLocalInput(broadcast.activeFrom),
    activeUntil: toLocalInput(broadcast.activeUntil),
  }
  error.value = ''
  isEditorOpen.value = true
}

function closeEditor() {
  isEditorOpen.value = false
  error.value = ''
}

async function submit() {
  error.value = ''
  try {
    await save.mutateAsync({
      id: editingId.value,
      body: {
        content: form.value.content.trim(),
        activeFrom: toIso(form.value.activeFrom),
        activeUntil: toIso(form.value.activeUntil),
      },
    })
    toast.success(editingId.value === null ? 'Broadcast scheduled.' : 'Broadcast updated.')
    isEditorOpen.value = false
  } catch (err) {
    error.value = messageFromContentError(err)
  }
}

async function destroy(broadcast: Broadcast) {
  try {
    await remove.mutateAsync(broadcast.id)
    toast.success('Broadcast deleted.')
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="space-y-1">
        <h2 class="text-sm font-semibold">Broadcasts</h2>
        <p class="text-sm text-muted-foreground">
          Announcements shown in-game on mod launch and as a banner on the website, for as long as
          their window lasts. Times are entered in your own timezone and stored as UTC.
        </p>
      </div>
      <Button type="button" variant="outline" size="sm" @click="startCreate">
        <Plus class="size-4" />
        New broadcast
      </Button>
    </div>

    <form v-if="isEditorOpen" class="space-y-4 rounded-md border p-4" @submit.prevent="submit">
      <div class="space-y-2">
        <Label for="broadcast-content">Message</Label>
        <textarea
          id="broadcast-content"
          v-model="form.content"
          rows="3"
          maxlength="511"
          class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        />
      </div>
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="space-y-2">
          <Label for="broadcast-from">Shown from</Label>
          <Input id="broadcast-from" v-model="form.activeFrom" type="datetime-local" />
        </div>
        <div class="space-y-2">
          <Label for="broadcast-until">Shown until</Label>
          <Input id="broadcast-until" v-model="form.activeUntil" type="datetime-local" />
        </div>
      </div>

      <div
        v-if="error"
        class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
      >
        {{ error }}
      </div>

      <div class="flex gap-2">
        <Button type="submit" :disabled="isSaving || !form.content.trim()">
          {{ isSaving ? 'Saving…' : editingId === null ? 'Schedule' : 'Save changes' }}
        </Button>
        <Button type="button" variant="outline" @click="closeEditor">
          <X class="size-4" />
          Cancel
        </Button>
      </div>
    </form>

    <p v-if="isLoading" class="text-sm text-muted-foreground">Loading…</p>
    <p v-else-if="broadcasts.length === 0" class="text-sm text-muted-foreground">
      No broadcasts scheduled.
    </p>

    <ul v-else class="space-y-2">
      <li
        v-for="broadcast in broadcasts"
        :key="broadcast.id"
        class="flex flex-wrap items-start gap-3 rounded-md border p-3 text-sm"
      >
        <div class="min-w-0 flex-1 space-y-1">
          <p class="break-words">{{ broadcast.content }}</p>
          <p class="text-xs text-muted-foreground">
            {{ new Date(broadcast.activeFrom).toLocaleString() }} –
            {{ new Date(broadcast.activeUntil).toLocaleString() }}
          </p>
        </div>
        <Badge :variant="isLive(broadcast) ? 'default' : 'secondary'">
          {{ isLive(broadcast) ? 'Live' : 'Inactive' }}
        </Badge>
        <div class="flex gap-1">
          <Button type="button" variant="ghost" size="icon-sm" @click="startEdit(broadcast)">
            <Pencil class="size-4" />
          </Button>
          <Button type="button" variant="ghost" size="icon-sm" @click="destroy(broadcast)">
            <Trash2 class="size-4" />
          </Button>
        </div>
      </li>
    </ul>
  </section>
</template>
