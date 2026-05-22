<script setup lang="ts">
import { computed, ref, useTemplateRef, watch } from 'vue'
import {
  Archive,
  ArchiveRestore,
  Edit,
  FileUp,
  Headphones,
  LinkIcon,
  Loader2,
  Trash2,
  Unlink,
  UserRound,
  X,
} from 'lucide-vue-next'
import {
  DialogClose,
  DialogContent,
  DialogOverlay,
  DialogPortal,
  DialogRoot,
  DialogTitle,
} from 'reka-ui'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { ContentOption, ContentSearchNpc, ContentSearchQuest } from '@/api/types'
import type { UploadNpcRecordingResult } from '@/api/types'
import { CONTENT_NONE, messageFromContentError, optionalContentId } from '../contentUtils'
import {
  useArchiveNpc,
  useDeleteNpcRecording,
  useDeleteQuest,
  useLinkQuestNpc,
  useNpcRecordings,
  useUnlinkQuestNpc,
  useUpdateNpc,
  useUpdateNpcVoiceActor,
  useUpdateQuest,
  useUpdateQuestNpcSoundEditor,
  useUpdateQuestWriter,
  useUploadNpcRecordings,
  useUploadQuestScript,
} from '../queries'
import NpcImageCropDialog from './NpcImageCropDialog.vue'

const NPC_IMAGE_BASE_URL = (import.meta.env.VITE_NPC_IMAGE_BASE_URL ?? '').replace(/\/$/, '')
const NPC_DEFAULT_IMAGE_URL = NPC_IMAGE_BASE_URL
  ? `${NPC_IMAGE_BASE_URL}/default.webp`
  : '/default.webp'

type DialogMode = 'quest' | 'npc' | null

const props = defineProps<{
  isLoading: boolean
  mode: DialogMode
  npcs: ContentOption[]
  open: boolean
  selectedNpc: ContentSearchNpc | null
  selectedQuest: ContentSearchQuest | null
  soundEditors: ContentOption[]
  voiceActors: ContentOption[]
  writers: ContentOption[]
}>()

const emit = defineEmits<{
  npcArchived: [replacementNpcId: number | null]
  'update:open': [value: boolean]
}>()

const updateQuestMutation = useUpdateQuest()
const updateQuestWriterMutation = useUpdateQuestWriter()
const deleteQuestMutation = useDeleteQuest()
const updateNpcMutation = useUpdateNpc()
const updateNpcVoiceActorMutation = useUpdateNpcVoiceActor()
const archiveNpcMutation = useArchiveNpc()
const updateQuestNpcSoundEditorMutation = useUpdateQuestNpcSoundEditor()
const linkQuestNpcMutation = useLinkQuestNpc()
const unlinkQuestNpcMutation = useUnlinkQuestNpc()
const uploadQuestScriptMutation = useUploadQuestScript()
const uploadNpcRecordingsMutation = useUploadNpcRecordings()
const deleteNpcRecordingMutation = useDeleteNpcRecording()

const editName = ref('')
const editWriter = ref(CONTENT_NONE)
const editVoiceActor = ref(CONTENT_NONE)
const editSoundEditor = ref(CONTENT_NONE)
const linkNpcId = ref(CONTENT_NONE)
const dialogError = ref('')
const scriptFile = ref<File | null>(null)
const scriptInput = useTemplateRef<HTMLInputElement>('scriptInput')
const imageInput = useTemplateRef<HTMLInputElement>('imageInput')
const recordingsInput = useTemplateRef<HTMLInputElement>('recordingsInput')
const pendingImageFile = ref<File | null>(null)
const cropDialogOpen = ref(false)
const imageBusters = ref<Map<number, number>>(new Map())
const recordingFiles = ref<File[]>([])
const recordingOverwrite = ref(false)
const recordingResults = ref<UploadNpcRecordingResult[]>([])
const isDraggingRecordings = ref(false)
const deletingRecordingId = ref<number | null>(null)
const archivingMode = ref<'replace' | 'archive-only' | null>(null)

const selectedQuestId = computed(() => props.selectedQuest?.questId ?? null)
const selectedNpcId = computed(() => props.selectedNpc?.npcId ?? null)
const shouldLoadNpcRecordings = computed(
  () =>
    props.open &&
    props.mode === 'npc' &&
    selectedQuestId.value !== null &&
    selectedNpcId.value !== null,
)
const npcRecordingsQuery = useNpcRecordings(selectedQuestId, selectedNpcId, shouldLoadNpcRecordings)
const npcRecordings = computed(() => npcRecordingsQuery.data.value ?? [])
const selectedNpcRecordingCount = computed(
  () => npcRecordingsQuery.data.value?.length ?? props.selectedNpc?.recordingCount ?? 0,
)
const selectedLinkNpc = computed(
  () => props.npcs.find((npc) => String(npc.id) === linkNpcId.value) ?? null,
)

const npcImageSrc = computed(() => {
  if (!props.selectedNpc) return NPC_DEFAULT_IMAGE_URL
  const url = `${NPC_IMAGE_BASE_URL}/${props.selectedNpc.npcId}.webp`
  const buster = imageBusters.value.get(props.selectedNpc.npcId)
  return buster ? `${url}${url.includes('?') ? '&' : '?'}v=${buster}` : url
})

function onImageError(event: Event) {
  const img = event.target as HTMLImageElement
  if (img.src !== NPC_DEFAULT_IMAGE_URL) {
    img.src = NPC_DEFAULT_IMAGE_URL
  }
}

function onImageFilePicked(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0] ?? null
  target.value = ''
  if (!file) return
  pendingImageFile.value = file
  cropDialogOpen.value = true
}

function onImageUploaded() {
  if (props.selectedNpc) {
    imageBusters.value.set(props.selectedNpc.npcId, Date.now())
  }
  pendingImageFile.value = null
}

function npcVoiceActorLabel(npc: ContentOption) {
  return npc.voiceActorName ? `Voice actor: ${npc.voiceActorName}` : 'No voice actor'
}

function npcOptionLabel(npc: ContentOption) {
  return `${npc.name} (${npc.id})`
}

function recordingResultDescription(result: UploadNpcRecordingResult) {
  if (!result.conflict) return result.description

  const conflict = result.conflict
  return `${result.description} Existing location: ${conflict.questName} (quest ${conflict.questId}) -> ${conflict.npcName} (NPC ${conflict.npcId}), line ${conflict.line}, recording ${conflict.recordingId} (${conflict.fileName}).`
}

watch(
  () => [props.mode, props.selectedQuest, props.selectedNpc, props.open] as const,
  () => {
    if (!props.open) return

    dialogError.value = ''
    linkNpcId.value = CONTENT_NONE
    scriptFile.value = null
    if (scriptInput.value) scriptInput.value.value = ''
    recordingFiles.value = []
    recordingOverwrite.value = false
    recordingResults.value = []
    if (recordingsInput.value) recordingsInput.value.value = ''
    pendingImageFile.value = null
    cropDialogOpen.value = false
    archivingMode.value = null
    if (imageInput.value) imageInput.value.value = ''

    if (props.mode === 'quest' && props.selectedQuest) {
      editName.value = props.selectedQuest.questName
      editWriter.value =
        props.selectedQuest.writerId === null ? CONTENT_NONE : String(props.selectedQuest.writerId)
    }

    if (props.mode === 'npc' && props.selectedNpc) {
      editName.value = props.selectedNpc.npcName
      editVoiceActor.value =
        props.selectedNpc.voiceActorId === null
          ? CONTENT_NONE
          : String(props.selectedNpc.voiceActorId)
      editSoundEditor.value =
        props.selectedNpc.soundEditorId === null
          ? CONTENT_NONE
          : String(props.selectedNpc.soundEditorId)
    }
  },
  { immediate: true },
)

function setOpen(value: boolean) {
  emit('update:open', value)
}

async function runDialogAction(action: () => Promise<void>, successMessage: string) {
  dialogError.value = ''
  try {
    await action()
    toast.success(successMessage)
  } catch (err) {
    dialogError.value = messageFromContentError(err)
  }
}

async function renameQuest() {
  if (!props.selectedQuest) return
  await runDialogAction(
    () =>
      updateQuestMutation.mutateAsync({
        questId: props.selectedQuest!.questId,
        request: { name: editName.value },
      }),
    'Quest renamed.',
  )
}

async function removeQuest() {
  if (!props.selectedQuest) return
  await runDialogAction(async () => {
    await deleteQuestMutation.mutateAsync(props.selectedQuest!.questId)
    setOpen(false)
  }, 'Quest deleted.')
}

async function saveWriter() {
  if (!props.selectedQuest) return
  await runDialogAction(
    () =>
      updateQuestWriterMutation.mutateAsync({
        questId: props.selectedQuest!.questId,
        request: { writerUserId: optionalContentId(editWriter.value) },
      }),
    'Writer updated.',
  )
}

async function linkNpc() {
  if (!props.selectedQuest) return
  const npcId = optionalContentId(linkNpcId.value)
  if (npcId === undefined) {
    dialogError.value = 'Select an NPC to link.'
    return
  }

  await runDialogAction(async () => {
    await linkQuestNpcMutation.mutateAsync({
      questId: props.selectedQuest!.questId,
      request: { npcId },
    })
    linkNpcId.value = CONTENT_NONE
  }, 'NPC linked to quest.')
}

async function renameNpc() {
  if (!props.selectedNpc) return
  await runDialogAction(
    () =>
      updateNpcMutation.mutateAsync({
        npcId: props.selectedNpc!.npcId,
        request: { name: editName.value },
      }),
    'NPC renamed.',
  )
}

async function saveVoiceActor() {
  if (!props.selectedNpc) return
  await runDialogAction(
    () =>
      updateNpcVoiceActorMutation.mutateAsync({
        npcId: props.selectedNpc!.npcId,
        request: { voiceActorUserId: optionalContentId(editVoiceActor.value) },
      }),
    'Voice actor updated.',
  )
}

async function saveSoundEditor() {
  if (!props.selectedQuest || !props.selectedNpc) return
  await runDialogAction(
    () =>
      updateQuestNpcSoundEditorMutation.mutateAsync({
        questId: props.selectedQuest!.questId,
        npcId: props.selectedNpc!.npcId,
        request: { soundEditorUserId: optionalContentId(editSoundEditor.value) },
      }),
    'Sound editor updated.',
  )
}

function onScriptFilePicked(event: Event) {
  const target = event.target as HTMLInputElement
  scriptFile.value = target.files?.[0] ?? null
}

function addRecordingFiles(files: FileList | File[]) {
  const nextFiles = Array.from(files)
  const accepted = nextFiles.filter((file) => file.name.toLowerCase().endsWith('.ogg'))
  const rejected = nextFiles.length - accepted.length

  if (rejected > 0) {
    dialogError.value = 'Only .ogg recording files can be selected.'
  }

  if (accepted.length === 0) return

  dialogError.value = ''
  const existingKeys = new Set(recordingFiles.value.map((file) => `${file.name}:${file.size}`))
  recordingFiles.value = [
    ...recordingFiles.value,
    ...accepted.filter((file) => !existingKeys.has(`${file.name}:${file.size}`)),
  ]
}

function onRecordingFilesPicked(event: Event) {
  const target = event.target as HTMLInputElement
  if (target.files) addRecordingFiles(target.files)
  target.value = ''
}

function onRecordingsDrop(event: DragEvent) {
  isDraggingRecordings.value = false
  if (event.dataTransfer?.files) addRecordingFiles(event.dataTransfer.files)
}

function removeRecordingFile(index: number) {
  recordingFiles.value = recordingFiles.value.filter((_, i) => i !== index)
}

async function uploadRecordings() {
  if (!props.selectedQuest || !props.selectedNpc || recordingFiles.value.length === 0) return
  const recordings = [...recordingFiles.value]
  await runDialogAction(async () => {
    const response = await uploadNpcRecordingsMutation.mutateAsync({
      questId: props.selectedQuest!.questId,
      npcId: props.selectedNpc!.npcId,
      recordings,
      overwrite: recordingOverwrite.value,
    })
    recordingResults.value = response.results
    recordingFiles.value = []
    if (recordingsInput.value) recordingsInput.value.value = ''
    await npcRecordingsQuery.refetch()
  }, 'Recordings processed.')
}

async function deleteRecording(recordingId: number) {
  if (!props.selectedQuest || !props.selectedNpc) return
  if (!window.confirm('Do you really want to delete this recording?')) return

  deletingRecordingId.value = recordingId
  await runDialogAction(async () => {
    await deleteNpcRecordingMutation.mutateAsync({
      questId: props.selectedQuest!.questId,
      npcId: props.selectedNpc!.npcId,
      recordingId,
    })
    await npcRecordingsQuery.refetch()
  }, 'Recording deleted.')
  deletingRecordingId.value = null
}

async function uploadScript() {
  if (!props.selectedQuest || !scriptFile.value) return
  const file = scriptFile.value
  await runDialogAction(async () => {
    await uploadQuestScriptMutation.mutateAsync({
      questId: props.selectedQuest!.questId,
      file,
    })
    scriptFile.value = null
    if (scriptInput.value) scriptInput.value.value = ''
  }, 'Script uploaded.')
}

async function unlinkNpc() {
  if (!props.selectedQuest || !props.selectedNpc) return
  await runDialogAction(async () => {
    await unlinkQuestNpcMutation.mutateAsync({
      questId: props.selectedQuest!.questId,
      npcId: props.selectedNpc!.npcId,
    })
    setOpen(false)
  }, 'NPC unlinked from quest.')
}

async function archiveNpc(createReplacement: boolean) {
  if (!props.selectedNpc) return

  const message = createReplacement
    ? 'Archive this NPC and create a replacement with the same name and picture?\n\nThe original NPC will be removed from current quest content, its recordings will be renamed as archived, and the replacement will have no voice actor or recordings.'
    : 'Archive this NPC without creating a replacement?\n\nThe NPC will be removed from all current quest content and its recordings will be renamed as archived. This cannot be undone from this page.'
  if (!window.confirm(message)) return

  archivingMode.value = createReplacement ? 'replace' : 'archive-only'
  await runDialogAction(async () => {
    const response = await archiveNpcMutation.mutateAsync({
      npcId: props.selectedNpc!.npcId,
      request: { createReplacement },
    })
    emit('npcArchived', response.replacementNpcId)
    if (response.replacementNpcId === null) {
      setOpen(false)
    }
  }, createReplacement ? 'NPC archived and replacement created.' : 'NPC archived.')
  archivingMode.value = null
}
</script>

<template>
  <DialogRoot :open="open" @update:open="setOpen">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-6xl -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
      >
        <div class="mb-4 flex items-start justify-between gap-4">
          <DialogTitle class="text-lg font-semibold">
            {{ mode === 'quest' ? 'Manage quest' : 'Manage NPC' }}
          </DialogTitle>
          <DialogClose
            aria-label="Close"
            class="rounded-md p-1 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          >
            <X class="size-4" />
          </DialogClose>
        </div>

        <div
          v-if="dialogError"
          class="mb-4 rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
        >
          {{ dialogError }}
        </div>

        <div v-if="mode === 'quest' && selectedQuest" class="space-y-5">
          <div class="space-y-2">
            <Label for="dialog-quest-name">Quest name</Label>
            <div class="flex gap-2">
              <Input id="dialog-quest-name" v-model="editName" maxlength="63" autocomplete="off" />
              <Button
                class="gap-2"
                :disabled="updateQuestMutation.isPending.value"
                @click="renameQuest"
              >
                <Edit class="size-4" />
                Save
              </Button>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="dialog-quest-writer">Writer</Label>
            <div class="flex gap-2">
              <Select v-model="editWriter" :disabled="isLoading">
                <SelectTrigger id="dialog-quest-writer" class="w-full">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem :value="CONTENT_NONE">None</SelectItem>
                  <SelectItem v-for="writer in writers" :key="writer.id" :value="String(writer.id)">
                    {{ writer.name }}
                  </SelectItem>
                </SelectContent>
              </Select>
              <Button
                class="gap-2"
                variant="outline"
                :disabled="updateQuestWriterMutation.isPending.value"
                @click="saveWriter"
              >
                <UserRound class="size-4" />
                Save
              </Button>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="dialog-quest-script">Script file (.txt)</Label>
            <div class="flex gap-2">
              <input
                id="dialog-quest-script"
                ref="scriptInput"
                type="file"
                accept="text/plain,.txt"
                class="border-input file:text-foreground h-9 w-full min-w-0 rounded-md border bg-transparent px-3 py-1 text-sm file:mr-3 file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium"
                @change="onScriptFilePicked"
              />
              <Button
                class="gap-2"
                variant="outline"
                :disabled="!scriptFile || uploadQuestScriptMutation.isPending.value"
                @click="uploadScript"
              >
                <FileUp class="size-4" />
                Upload
              </Button>
            </div>
            <p v-if="selectedQuest.scriptUrl" class="text-sm text-muted-foreground">
              Current:
              <a :href="selectedQuest.scriptUrl" target="_blank" rel="noopener" class="underline">
                View current script
              </a>
            </p>
            <p v-else class="text-sm text-muted-foreground">No script uploaded.</p>
          </div>

          <div class="space-y-2">
            <Label for="dialog-link-npc">Add NPC to quest</Label>
            <div class="flex gap-2">
              <Select v-model="linkNpcId" :disabled="isLoading">
                <SelectTrigger id="dialog-link-npc" class="w-full">
                  <SelectValue placeholder="Select NPC">
                    <span v-if="selectedLinkNpc" class="truncate">
                      {{ npcOptionLabel(selectedLinkNpc) }}
                      <span class="text-muted-foreground">
                        ({{ npcVoiceActorLabel(selectedLinkNpc) }})
                      </span>
                    </span>
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  <SelectItem :value="CONTENT_NONE">Select NPC</SelectItem>
                  <SelectItem
                    v-for="npc in npcs"
                    :key="npc.id"
                    :value="String(npc.id)"
                    class="py-2"
                  >
                    <span class="flex min-w-0 flex-col items-start gap-0.5">
                      <span class="max-w-full truncate">{{ npcOptionLabel(npc) }}</span>
                      <span class="max-w-full truncate text-xs text-muted-foreground">
                        {{ npcVoiceActorLabel(npc) }}
                      </span>
                    </span>
                  </SelectItem>
                </SelectContent>
              </Select>
              <Button
                class="gap-2"
                variant="outline"
                :disabled="linkQuestNpcMutation.isPending.value"
                @click="linkNpc"
              >
                <LinkIcon class="size-4" />
                Link
              </Button>
            </div>
          </div>

          <div
            class="flex flex-col gap-3 border-t pt-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <p v-if="selectedQuest.npcs.length > 0" class="text-sm text-muted-foreground">
              Deleting is blocked because this quest has
              {{ selectedQuest.npcs.length }}
              linked {{ selectedQuest.npcs.length === 1 ? 'NPC' : 'NPCs' }}.
            </p>
            <div v-else />
            <Button
              variant="destructive"
              class="gap-2 self-start sm:self-auto"
              :disabled="selectedQuest.npcs.length > 0 || deleteQuestMutation.isPending.value"
              @click="removeQuest"
            >
              <Trash2 class="size-4" />
              Delete quest
            </Button>
          </div>
        </div>

        <div v-else-if="mode === 'npc' && selectedQuest && selectedNpc" class="space-y-5">
          <div>
            <p class="text-sm text-muted-foreground">Quest</p>
            <p class="font-medium">{{ selectedQuest.questName }}</p>
          </div>

          <div class="flex items-start gap-4">
            <img
              :key="npcImageSrc"
              :src="npcImageSrc"
              alt="NPC picture"
              class="size-24 shrink-0 rounded-md border bg-muted object-cover"
              @error="onImageError"
            />
            <div class="flex-1 space-y-2">
              <Label for="dialog-npc-image">Picture</Label>
              <input
                id="dialog-npc-image"
                ref="imageInput"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                class="border-input file:text-foreground h-9 w-full min-w-0 rounded-md border bg-transparent px-3 py-1 text-sm file:mr-3 file:inline-flex file:h-7 file:border-0 file:bg-transparent file:text-sm file:font-medium"
                @change="onImageFilePicked"
              />
              <p class="text-xs text-muted-foreground">
                After picking a file you can crop and zoom; the image will be saved as 256×256 webp.
              </p>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="dialog-npc-name">NPC name</Label>
            <div class="flex gap-2">
              <Input id="dialog-npc-name" v-model="editName" maxlength="63" autocomplete="off" />
              <Button
                class="gap-2"
                :disabled="updateNpcMutation.isPending.value"
                @click="renameNpc"
              >
                <Edit class="size-4" />
                Save
              </Button>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="dialog-voice-actor">Voice actor</Label>
            <div class="flex gap-2">
              <Select v-model="editVoiceActor" :disabled="isLoading">
                <SelectTrigger id="dialog-voice-actor" class="w-full">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem :value="CONTENT_NONE">None</SelectItem>
                  <SelectItem
                    v-for="actor in voiceActors"
                    :key="actor.id"
                    :value="String(actor.id)"
                  >
                    {{ actor.name }}
                  </SelectItem>
                </SelectContent>
              </Select>
              <Button
                class="gap-2"
                variant="outline"
                :disabled="updateNpcVoiceActorMutation.isPending.value"
                @click="saveVoiceActor"
              >
                <UserRound class="size-4" />
                Save
              </Button>
            </div>
          </div>

          <div class="space-y-2">
            <Label for="dialog-sound-editor">Sound editor</Label>
            <div class="flex gap-2">
              <Select v-model="editSoundEditor" :disabled="isLoading">
                <SelectTrigger id="dialog-sound-editor" class="w-full">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem :value="CONTENT_NONE">None</SelectItem>
                  <SelectItem
                    v-for="editor in soundEditors"
                    :key="editor.id"
                    :value="String(editor.id)"
                  >
                    {{ editor.name }}
                  </SelectItem>
                </SelectContent>
              </Select>
              <Button
                class="gap-2"
                variant="outline"
                :disabled="updateQuestNpcSoundEditorMutation.isPending.value"
                @click="saveSoundEditor"
              >
                <Headphones class="size-4" />
                Save
              </Button>
            </div>
          </div>

          <div class="space-y-3 border-t pt-4">
            <div class="flex items-center justify-between gap-3">
              <Label for="dialog-npc-recordings">Recordings (.ogg)</Label>
              <label class="flex items-center gap-2 text-sm text-muted-foreground">
                <input
                  v-model="recordingOverwrite"
                  type="checkbox"
                  class="size-4 rounded border-input"
                />
                Overwrite existing files
              </label>
            </div>

            <div
              class="rounded-md border border-dashed p-4 transition-colors"
              :class="isDraggingRecordings ? 'border-primary bg-primary/5' : 'border-input'"
              @dragenter.prevent="isDraggingRecordings = true"
              @dragover.prevent="isDraggingRecordings = true"
              @dragleave.prevent="isDraggingRecordings = false"
              @drop.prevent="onRecordingsDrop"
            >
              <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p class="text-sm font-medium">Drop .ogg files here</p>
                  <p class="text-xs text-muted-foreground">
                    Filenames must end with the line number, such as 1.ogg or quest-npc-42.ogg.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  class="gap-2 self-start sm:self-auto"
                  @click="recordingsInput?.click()"
                >
                  <FileUp class="size-4" />
                  Choose files
                </Button>
              </div>
              <input
                id="dialog-npc-recordings"
                ref="recordingsInput"
                type="file"
                multiple
                accept=".ogg,audio/ogg"
                class="sr-only"
                @change="onRecordingFilesPicked"
              />
            </div>

            <div v-if="recordingFiles.length > 0" class="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
              <div
                v-for="(file, index) in recordingFiles"
                :key="`${file.name}-${file.size}-${index}`"
                class="flex min-w-0 items-center justify-between gap-3 rounded-md border px-3 py-2 text-sm"
              >
                <span class="min-w-0 truncate">{{ file.name }}</span>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  aria-label="Remove recording"
                  @click="removeRecordingFile(index)"
                >
                  <X class="size-4" />
                </Button>
              </div>
            </div>

            <div class="flex justify-end">
              <Button
                class="gap-2"
                :disabled="
                  recordingFiles.length === 0 || uploadNpcRecordingsMutation.isPending.value
                "
                @click="uploadRecordings"
              >
                <FileUp class="size-4" />
                Upload recordings
              </Button>
            </div>

            <div v-if="recordingResults.length > 0" class="grid gap-2 lg:grid-cols-2">
              <div
                v-for="result in recordingResults"
                :key="`${result.fileName}-${result.code}`"
                class="min-w-0 rounded-md border px-3 py-2 text-sm"
              >
                <div class="flex items-center justify-between gap-3">
                  <span class="min-w-0 truncate font-medium">{{ result.fileName }}</span>
                  <span
                    class="shrink-0 rounded-sm px-2 py-0.5 text-xs font-medium"
                    :class="
                      result.code >= 200 && result.code < 300
                        ? 'bg-emerald-500/10 text-emerald-700'
                        : 'bg-destructive/10 text-destructive'
                    "
                  >
                    {{ result.code }} {{ result.message }}
                  </span>
                </div>
                <p class="mt-1 text-xs text-muted-foreground">
                  {{ recordingResultDescription(result) }}
                </p>
              </div>
            </div>
          </div>

          <div class="space-y-3 border-t pt-4">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium">Existing recordings</p>
                <p class="text-xs text-muted-foreground">
                  Recordings linked to this quest and NPC.
                </p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                :disabled="npcRecordingsQuery.isFetching.value"
                @click="npcRecordingsQuery.refetch()"
              >
                Refresh
              </Button>
            </div>

            <div
              v-if="npcRecordingsQuery.isLoading.value"
              class="flex items-center gap-2 rounded-md border px-3 py-2 text-sm text-muted-foreground"
            >
              <Loader2 class="size-4 animate-spin" />
              Loading recordings...
            </div>
            <div
              v-else-if="npcRecordingsQuery.isError.value"
              class="rounded-md border border-destructive/50 bg-destructive/5 px-3 py-2 text-sm text-destructive"
            >
              Existing recordings could not be loaded.
            </div>
            <div
              v-else-if="npcRecordings.length === 0"
              class="rounded-md border px-3 py-2 text-sm text-muted-foreground"
            >
              No recordings uploaded for this NPC in this quest.
            </div>
            <div v-else class="grid gap-2 xl:grid-cols-2">
              <div
                v-for="recording in npcRecordings"
                :key="recording.recordingId"
                class="grid min-w-0 gap-2 rounded-md border px-3 py-2 text-sm sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center"
              >
                <div class="min-w-0 space-y-2">
                  <div class="flex items-center gap-2">
                    <span class="font-medium">Line {{ recording.line }}</span>
                    <span class="min-w-0 truncate text-muted-foreground">
                      {{ recording.fileName }}
                    </span>
                  </div>
                  <audio controls preload="none" :src="recording.url" class="h-9 w-full min-w-0" />
                </div>
                <Button
                  type="button"
                  size="icon"
                  variant="destructive"
                  aria-label="Delete recording"
                  :disabled="deletingRecordingId === recording.recordingId"
                  @click="deleteRecording(recording.recordingId)"
                >
                  <Loader2
                    v-if="deletingRecordingId === recording.recordingId"
                    class="size-4 animate-spin"
                  />
                  <Trash2 v-else class="size-4" />
                </Button>
              </div>
            </div>
          </div>

          <div
            class="flex flex-col gap-3 border-t pt-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <p v-if="selectedNpcRecordingCount > 0" class="text-sm text-muted-foreground">
              Unlinking is blocked because this NPC has
              {{ selectedNpcRecordingCount }}
              {{ selectedNpcRecordingCount === 1 ? 'recording' : 'recordings' }}
              in this quest.
            </p>
            <div v-else />
            <Button
              variant="destructive"
              class="gap-2 self-start sm:self-auto"
              :disabled="selectedNpcRecordingCount > 0 || unlinkQuestNpcMutation.isPending.value"
              @click="unlinkNpc"
            >
              <Unlink class="size-4" />
              Unlink NPC
            </Button>
          </div>

          <div class="space-y-3 border-t pt-4">
            <div>
              <p class="text-sm font-medium">Archive NPC</p>
              <p class="text-xs text-muted-foreground">
                Archiving removes this NPC from current content and marks its recordings as outdated.
              </p>
            </div>
            <div class="flex flex-col gap-2 sm:flex-row sm:justify-end">
              <Button
                variant="outline"
                class="gap-2 self-start sm:self-auto"
                :disabled="archiveNpcMutation.isPending.value"
                @click="archiveNpc(true)"
              >
                <Loader2
                  v-if="archivingMode === 'replace'"
                  class="size-4 animate-spin"
                />
                <ArchiveRestore v-else class="size-4" />
                Archive and recreate
              </Button>
              <Button
                variant="destructive"
                class="gap-2 self-start sm:self-auto"
                :disabled="archiveNpcMutation.isPending.value"
                @click="archiveNpc(false)"
              >
                <Loader2
                  v-if="archivingMode === 'archive-only'"
                  class="size-4 animate-spin"
                />
                <Archive v-else class="size-4" />
                Archive only
              </Button>
            </div>
          </div>
        </div>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>

  <NpcImageCropDialog
    v-model:open="cropDialogOpen"
    :npc-id="selectedNpc?.npcId ?? null"
    :source="pendingImageFile"
    @uploaded="onImageUploaded"
  />
</template>
