<script setup lang="ts">
import { computed, ref, useTemplateRef, watch } from 'vue'
import {
  Edit,
  FileUp,
  Headphones,
  LinkIcon,
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
import { CONTENT_NONE, messageFromContentError, optionalContentId } from '../contentUtils'
import {
  useDeleteQuest,
  useLinkQuestNpc,
  useUnlinkQuestNpc,
  useUpdateNpc,
  useUpdateNpcVoiceActor,
  useUpdateQuest,
  useUpdateQuestNpcSoundEditor,
  useUpdateQuestWriter,
  useUploadQuestScript,
} from '../queries'
import NpcImageCropDialog from './NpcImageCropDialog.vue'

const NPC_IMAGE_BASE_URL = import.meta.env.VITE_NPC_IMAGE_BASE_URL.replace(/\/$/, '')
const NPC_DEFAULT_IMAGE_URL = `${NPC_IMAGE_BASE_URL}/default.webp`

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
  'update:open': [value: boolean]
}>()

const updateQuestMutation = useUpdateQuest()
const updateQuestWriterMutation = useUpdateQuestWriter()
const deleteQuestMutation = useDeleteQuest()
const updateNpcMutation = useUpdateNpc()
const updateNpcVoiceActorMutation = useUpdateNpcVoiceActor()
const updateQuestNpcSoundEditorMutation = useUpdateQuestNpcSoundEditor()
const linkQuestNpcMutation = useLinkQuestNpc()
const unlinkQuestNpcMutation = useUnlinkQuestNpc()
const uploadQuestScriptMutation = useUploadQuestScript()

const editName = ref('')
const editWriter = ref(CONTENT_NONE)
const editVoiceActor = ref(CONTENT_NONE)
const editSoundEditor = ref(CONTENT_NONE)
const linkNpcId = ref(CONTENT_NONE)
const dialogError = ref('')
const scriptFile = ref<File | null>(null)
const scriptInput = useTemplateRef<HTMLInputElement>('scriptInput')
const imageInput = useTemplateRef<HTMLInputElement>('imageInput')
const pendingImageFile = ref<File | null>(null)
const cropDialogOpen = ref(false)
const imageBusters = ref<Map<number, number>>(new Map())

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

watch(
  () => [props.mode, props.selectedQuest, props.selectedNpc, props.open] as const,
  () => {
    if (!props.open) return

    dialogError.value = ''
    linkNpcId.value = CONTENT_NONE
    scriptFile.value = null
    if (scriptInput.value) scriptInput.value.value = ''
    pendingImageFile.value = null
    cropDialogOpen.value = false
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
</script>

<template>
  <DialogRoot :open="open" @update:open="setOpen">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/45" />
      <DialogContent
        class="fixed left-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100vw-2rem)] max-w-xl -translate-x-1/2 -translate-y-1/2 overflow-auto rounded-md border bg-background p-5 shadow-lg"
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
                  <SelectItem
                    v-for="writer in writers"
                    :key="writer.id"
                    :value="String(writer.id)"
                  >
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
              <a
                :href="selectedQuest.scriptUrl"
                target="_blank"
                rel="noopener"
                class="underline"
              >
                View current script
              </a>
            </p>
            <p v-else class="text-sm text-muted-foreground">No script uploaded.</p>
          </div>

          <div class="space-y-2">
            <Label for="dialog-link-npc">Link existing NPC</Label>
            <div class="flex gap-2">
              <Select v-model="linkNpcId" :disabled="isLoading">
                <SelectTrigger id="dialog-link-npc" class="w-full">
                  <SelectValue placeholder="Select NPC" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem :value="CONTENT_NONE">Select NPC</SelectItem>
                  <SelectItem v-for="npc in npcs" :key="npc.id" :value="String(npc.id)">
                    {{ npc.name }}
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

          <div
            class="flex flex-col gap-3 border-t pt-4 sm:flex-row sm:items-center sm:justify-between"
          >
            <p v-if="selectedNpc.recordingCount > 0" class="text-sm text-muted-foreground">
              Unlinking is blocked because this NPC has
              {{ selectedNpc.recordingCount }}
              {{ selectedNpc.recordingCount === 1 ? 'recording' : 'recordings' }}
              in this quest.
            </p>
            <div v-else />
            <Button
              variant="destructive"
              class="gap-2 self-start sm:self-auto"
              :disabled="selectedNpc.recordingCount > 0 || unlinkQuestNpcMutation.isPending.value"
              @click="unlinkNpc"
            >
              <Unlink class="size-4" />
              Unlink NPC
            </Button>
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
