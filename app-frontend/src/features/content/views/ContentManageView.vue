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
import { ApiError } from '@/api/client'
import type { ContentOption, CreateNpcQuestAssignmentRequest } from '@/api/types'
import { useContentOptions, useCreateNpc, useCreateQuest } from '../queries'

type Tab = 'quest' | 'npc'

interface ValidationProblem {
  errors?: Record<string, string[]>
  title?: string
}

const NONE = 'none'

const activeTab = ref<Tab>('quest')
const questName = ref('')
const questWriter = ref(NONE)
const npcName = ref('')
const npcVoiceActor = ref(NONE)
const selectedQuestIds = ref<number[]>([])
const questEditorSelections = ref<Record<number, string>>({})
const questError = ref('')
const npcError = ref('')
const createdQuestId = ref<number | null>(null)
const createdNpcId = ref<number | null>(null)

const { data: options, isLoading, isError, error } = useContentOptions()
const createQuestMutation = useCreateQuest()
const createNpcMutation = useCreateNpc()

const quests = computed(() => options.value?.quests ?? [])
const writers = computed(() => options.value?.writers ?? [])
const voiceActors = computed(() => options.value?.voiceActors ?? [])
const soundEditors = computed(() => options.value?.soundEditors ?? [])

const selectedQuests = computed(() =>
  selectedQuestIds.value
    .map((id) => quests.value.find((quest) => quest.id === id))
    .filter((quest): quest is ContentOption => Boolean(quest)),
)

function optionalId(value: string): number | undefined {
  return value === NONE ? undefined : Number(value)
}

function messageFromError(err: unknown): string {
  if (err instanceof ApiError) {
    const body = err.body as ValidationProblem | null
    const firstError = body?.errors ? Object.values(body.errors)[0]?.[0] : undefined
    return firstError ?? body?.title ?? err.message
  }

  return err instanceof Error ? err.message : 'Unknown error'
}

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

async function submitQuest() {
  questError.value = ''
  createdQuestId.value = null

  try {
    const result = await createQuestMutation.mutateAsync({
      name: questName.value,
      writerUserId: optionalId(questWriter.value),
    })
    createdQuestId.value = result.id
    questName.value = ''
    questWriter.value = NONE
    toast.success(`Quest created with ID ${result.id}.`)
  } catch (err) {
    questError.value = messageFromError(err)
  }
}

async function submitNpc() {
  npcError.value = ''
  createdNpcId.value = null

  const questAssignments: CreateNpcQuestAssignmentRequest[] = selectedQuestIds.value.map((questId) => ({
    questId,
    soundEditorUserId: optionalId(questEditorSelections.value[questId] ?? NONE),
  }))

  try {
    const result = await createNpcMutation.mutateAsync({
      name: npcName.value,
      voiceActorUserId: optionalId(npcVoiceActor.value),
      questAssignments,
    })
    createdNpcId.value = result.id
    npcName.value = ''
    npcVoiceActor.value = NONE
    selectedQuestIds.value = []
    questEditorSelections.value = {}
    toast.success(`NPC created with ID ${result.id}.`)
  } catch (err) {
    npcError.value = messageFromError(err)
  }
}
</script>

<template>
  <div class="mx-auto max-w-screen-xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Manage content</h1>
      <p class="text-sm text-muted-foreground">Create quests and NPCs for website content.</p>
    </header>

    <div class="inline-flex rounded-md border bg-background p-1">
      <button
        type="button"
        class="rounded-sm px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors"
        :class="{ 'bg-accent text-accent-foreground': activeTab === 'quest' }"
        @click="activeTab = 'quest'"
      >
        Create quest
      </button>
      <button
        type="button"
        class="rounded-sm px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors"
        :class="{ 'bg-accent text-accent-foreground': activeTab === 'npc' }"
        @click="activeTab = 'npc'"
      >
        Create NPC
      </button>
    </div>

    <div v-if="isError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
      Failed to load content options: {{ messageFromError(error) }}
    </div>

    <form
      v-if="activeTab === 'quest'"
      class="max-w-2xl space-y-5 rounded-md border bg-background p-5"
      @submit.prevent="submitQuest"
    >
      <div class="space-y-2">
        <Label for="quest-name">Quest name</Label>
        <Input id="quest-name" v-model="questName" maxlength="63" autocomplete="off" />
      </div>

      <div class="space-y-2">
        <Label for="quest-writer">Writer</Label>
        <Select v-model="questWriter" :disabled="isLoading">
          <SelectTrigger id="quest-writer" class="w-full">
            <SelectValue placeholder="None" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem :value="NONE">None</SelectItem>
            <SelectItem v-for="writer in writers" :key="writer.id" :value="String(writer.id)">
              {{ writer.name }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div v-if="questError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
        {{ questError }}
      </div>
      <div v-if="createdQuestId" class="rounded-md border border-emerald-600/30 bg-emerald-50 p-3 text-sm text-emerald-800">
        Quest created with ID {{ createdQuestId }}.
      </div>

      <Button type="submit" :disabled="createQuestMutation.isPending.value || isLoading">
        Create quest
      </Button>
    </form>

    <form
      v-else
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
              <SelectItem :value="NONE">None</SelectItem>
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

        <div v-if="selectedQuests.length === 0" class="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          Select a quest to assign editors.
        </div>

        <div v-for="quest in selectedQuests" :key="quest.id" class="space-y-2 rounded-md border p-3">
          <Label :for="`editor-${quest.id}`">{{ quest.name }}</Label>
          <Select v-model="questEditorSelections[quest.id]" :disabled="isLoading">
            <SelectTrigger :id="`editor-${quest.id}`" class="w-full">
              <SelectValue placeholder="None" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem :value="NONE">None</SelectItem>
              <SelectItem v-for="editor in soundEditors" :key="editor.id" :value="String(editor.id)">
                {{ editor.name }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div v-if="npcError" class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive">
          {{ npcError }}
        </div>
        <div v-if="createdNpcId" class="rounded-md border border-emerald-600/30 bg-emerald-50 p-3 text-sm text-emerald-800">
          NPC created with ID {{ createdNpcId }}.
        </div>

        <Button type="submit" :disabled="createNpcMutation.isPending.value || isLoading">
          Create NPC
        </Button>
      </div>
    </form>
  </div>
</template>
