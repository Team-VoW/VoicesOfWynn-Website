<script setup lang="ts">
import { computed, ref } from 'vue'
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
import type { ContentOption, CreateNpcQuestAssignmentRequest } from '@/api/types'
import { CONTENT_NONE, messageFromContentError, optionalContentId } from '../contentUtils'
import { useCreateNpc } from '../queries'

const props = defineProps<{
  isLoading: boolean
  quests: ContentOption[]
  soundEditors: ContentOption[]
  voiceActors: ContentOption[]
}>()

const npcName = ref('')
const npcVoiceActor = ref(CONTENT_NONE)
const selectedQuestIds = ref<number[]>([])
const questEditorSelections = ref<Record<number, string>>({})
const npcError = ref('')
const createdNpcId = ref<number | null>(null)
const createNpcMutation = useCreateNpc()

const selectedQuests = computed(() =>
  selectedQuestIds.value
    .map((id) => props.quests.find((quest) => quest.id === id))
    .filter((quest): quest is ContentOption => Boolean(quest)),
)

function toggleQuest(questId: number, checked: boolean) {
  if (checked) {
    if (!selectedQuestIds.value.includes(questId)) {
      selectedQuestIds.value = [...selectedQuestIds.value, questId]
    }
    return
  }

  selectedQuestIds.value = selectedQuestIds.value.filter((id) => id !== questId)
  const next = { ...questEditorSelections.value }
  delete next[questId]
  questEditorSelections.value = next
}

async function submitNpc() {
  npcError.value = ''
  createdNpcId.value = null

  const questAssignments: CreateNpcQuestAssignmentRequest[] = selectedQuestIds.value.map(
    (questId) => ({
      questId,
      soundEditorUserId: optionalContentId(questEditorSelections.value[questId] ?? CONTENT_NONE),
    }),
  )

  try {
    const result = await createNpcMutation.mutateAsync({
      name: npcName.value,
      voiceActorUserId: optionalContentId(npcVoiceActor.value),
      questAssignments,
    })
    createdNpcId.value = result.id
    npcName.value = ''
    npcVoiceActor.value = CONTENT_NONE
    selectedQuestIds.value = []
    questEditorSelections.value = {}
    toast.success(`NPC created with ID ${result.id}.`)
  } catch (err) {
    npcError.value = messageFromContentError(err)
  }
}
</script>

<template>
  <form
    class="grid gap-5 rounded-md border bg-background p-5 lg:grid-cols-[minmax(0,1fr)_minmax(20rem,28rem)]"
    @submit.prevent="submitNpc"
  >
    <div class="space-y-5">
      <div class="space-y-2">
        <Label for="npc-name">NPC name</Label>
        <Input id="npc-name" v-model="npcName" maxlength="63" autocomplete="off" />
      </div>

      <div class="space-y-2">
        <Label for="npc-voice-actor">Voice actor</Label>
        <Select v-model="npcVoiceActor" :disabled="isLoading">
          <SelectTrigger id="npc-voice-actor" class="w-full">
            <SelectValue placeholder="None" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem :value="CONTENT_NONE">None</SelectItem>
            <SelectItem v-for="actor in voiceActors" :key="actor.id" :value="String(actor.id)">
              {{ actor.name }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div class="space-y-2">
        <Label>Quests</Label>
        <div class="max-h-80 overflow-auto rounded-md border">
          <label
            v-for="quest in quests"
            :key="quest.id"
            class="flex items-center gap-3 border-b px-3 py-2 text-sm last:border-b-0"
          >
            <input
              type="checkbox"
              class="size-4 rounded border-input"
              :checked="selectedQuestIds.includes(quest.id)"
              @change="toggleQuest(quest.id, ($event.target as HTMLInputElement).checked)"
            />
            <span>{{ quest.name }}</span>
          </label>
          <div v-if="!isLoading && quests.length === 0" class="p-3 text-sm text-muted-foreground">
            No quests are available.
          </div>
          <div v-if="isLoading" class="p-3 text-sm text-muted-foreground">Loading quests...</div>
        </div>
      </div>
    </div>

    <div class="space-y-4">
      <div>
        <h2 class="text-sm font-medium">Sound editors</h2>
        <p class="text-sm text-muted-foreground">Optional editor assignment per selected quest.</p>
      </div>

      <div
        v-if="selectedQuests.length === 0"
        class="rounded-md border border-dashed p-4 text-sm text-muted-foreground"
      >
        Select a quest to assign editors.
      </div>

      <div v-for="quest in selectedQuests" :key="quest.id" class="space-y-2 rounded-md border p-3">
        <Label :for="`editor-${quest.id}`">{{ quest.name }}</Label>
        <Select v-model="questEditorSelections[quest.id]" :disabled="isLoading">
          <SelectTrigger :id="`editor-${quest.id}`" class="w-full">
            <SelectValue placeholder="None" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem :value="CONTENT_NONE">None</SelectItem>
            <SelectItem v-for="editor in soundEditors" :key="editor.id" :value="String(editor.id)">
              {{ editor.name }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div
        v-if="npcError"
        class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
      >
        {{ npcError }}
      </div>
      <div
        v-if="createdNpcId"
        class="rounded-md border border-emerald-600/30 bg-emerald-50 p-3 text-sm text-emerald-800"
      >
        NPC created with ID {{ createdNpcId }}.
      </div>

      <Button type="submit" :disabled="createNpcMutation.isPending.value || isLoading">
        Create NPC
      </Button>
    </div>
  </form>
</template>
