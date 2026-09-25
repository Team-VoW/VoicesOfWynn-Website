<script setup lang="ts">
import { ref } from 'vue'
import { toast } from 'vue-sonner'
import { Crown, Pencil, Trash2, Upload } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import SeekableAudioPlayer from '@/components/audio/SeekableAudioPlayer.vue'
import type { AdminCastingCharacter, SaveCastingCharacterRequest } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useDeleteCastingAudition, useDeleteCastingCharacter, useUploadCastingAudition } from '../queries'
import CharacterForm from './CharacterForm.vue'

const props = defineProps<{
  character: AdminCastingCharacter
  editable: boolean
  saving: boolean
}>()

const emit = defineEmits<{ save: [body: SaveCastingCharacterRequest, done: (error: string) => void] }>()

const upload = useUploadCastingAudition()
const removeAudition = useDeleteCastingAudition()
const removeCharacter = useDeleteCastingCharacter()

const editing = ref(false)
const editError = ref('')
const confirmDelete = ref(false)
const auditioneeName = ref('')
const file = ref<File | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

function save(body: SaveCastingCharacterRequest) {
  editError.value = ''
  emit('save', body, (error) => {
    editError.value = error
    if (!error) editing.value = false
  })
}

async function submitUpload() {
  if (!file.value || !auditioneeName.value.trim()) return
  try {
    await upload.mutateAsync({
      characterId: props.character.id,
      auditioneeName: auditioneeName.value.trim(),
      file: file.value,
    })
    toast.success('Audition added.')
    auditioneeName.value = ''
    file.value = null
    if (fileInput.value) fileInput.value.value = ''
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}

async function deleteAudition(id: number) {
  try {
    await removeAudition.mutateAsync(id)
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}

async function deleteCharacter() {
  if (!confirmDelete.value) {
    confirmDelete.value = true
    setTimeout(() => (confirmDelete.value = false), 4000)
    return
  }
  try {
    await removeCharacter.mutateAsync(props.character.id)
    toast.success(`${props.character.name} removed.`)
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <article class="space-y-3 rounded-lg border bg-background p-4">
    <CharacterForm
      v-if="editing"
      :initial="character"
      :saving="saving"
      :error="editError"
      submit-label="Save character"
      @submit="save"
      @cancel="editing = false"
    />
    <header v-else class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0 space-y-1">
        <div class="flex flex-wrap items-center gap-2">
          <h3 class="font-semibold">{{ character.name }}</h3>
          <span
            v-if="character.questName"
            class="rounded-full border border-primary/25 px-2 py-0.5 text-xs text-primary"
          >
            {{ character.questName }}
          </span>
          <span class="text-xs text-muted-foreground">{{ character.auditions.length }} auditions</span>
        </div>
        <p v-if="character.direction" class="max-w-2xl text-sm text-muted-foreground">
          {{ character.direction }}
        </p>
        <p v-else class="text-sm text-muted-foreground/70 italic">No direction yet.</p>
      </div>
      <div class="flex gap-1">
        <Button type="button" variant="ghost" size="icon-sm" :aria-label="`Edit ${character.name}`" @click="editing = true">
          <Pencil class="size-4" />
        </Button>
        <Button
          v-if="editable"
          type="button"
          variant="ghost"
          :size="confirmDelete ? 'sm' : 'icon-sm'"
          :class="confirmDelete ? 'text-destructive' : ''"
          :aria-label="`Delete ${character.name}`"
          @click="deleteCharacter"
        >
          <Trash2 class="size-4" />
          <template v-if="confirmDelete">Delete with {{ character.auditions.length }} auditions?</template>
        </Button>
      </div>
    </header>

    <ul v-if="character.auditions.length > 0" class="divide-y rounded-md border">
      <li v-for="audition in character.auditions" :key="audition.id" class="flex items-center gap-3 px-3 py-2">
        <span class="w-6 text-xs text-muted-foreground tabular-nums">{{ audition.number }}</span>
        <span class="w-40 min-w-0 truncate text-sm">
          {{ audition.auditioneeName }}
          <Crown
            v-if="character.winnerAuditionId === audition.id"
            class="ml-1 inline size-3.5 text-amber-600"
            aria-label="Winner"
          />
        </span>
        <SeekableAudioPlayer
          class="flex-1"
          :src="audition.audioUrl"
          :label="`audition ${audition.number} by ${audition.auditioneeName}`"
          :duration-seconds="audition.durationSeconds"
        />
        <Button
          v-if="editable"
          type="button"
          variant="ghost"
          size="icon-sm"
          :aria-label="`Delete audition ${audition.number}`"
          :disabled="removeAudition.isPending.value"
          @click="deleteAudition(audition.id)"
        >
          <Trash2 class="size-4" />
        </Button>
      </li>
    </ul>

    <form v-if="editable" class="flex flex-wrap items-center gap-2" @submit.prevent="submitUpload">
      <Input
        v-model="auditioneeName"
        placeholder="Auditionee name"
        aria-label="Auditionee name"
        maxlength="100"
        class="h-8 w-48"
      />
      <input
        ref="fileInput"
        type="file"
        accept="audio/*,.ogg,.wav,.mp3,.m4a,.flac"
        aria-label="Audition audio file"
        class="max-w-64 text-xs file:mr-2 file:cursor-pointer file:rounded-md file:border file:bg-background file:px-2 file:py-1 file:text-xs"
        @change="file = ($event.target as HTMLInputElement).files?.[0] ?? null"
      />
      <Button type="submit" size="sm" variant="outline" :disabled="!file || !auditioneeName.trim() || upload.isPending.value">
        <Upload class="size-4" />
        {{ upload.isPending.value ? 'Uploading…' : 'Add audition' }}
      </Button>
    </form>
  </article>
</template>
