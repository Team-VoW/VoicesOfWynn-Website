<script setup lang="ts">
import { ref } from 'vue'
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
import type { ContentOption } from '@/api/types'
import { CONTENT_NONE, messageFromContentError, optionalContentId } from '../contentUtils'
import { useCreateQuest } from '../queries'

defineProps<{
  isLoading: boolean
  writers: ContentOption[]
}>()

const questName = ref('')
const questWriter = ref(CONTENT_NONE)
const questError = ref('')
const createdQuestId = ref<number | null>(null)
const createQuestMutation = useCreateQuest()

async function submitQuest() {
  questError.value = ''
  createdQuestId.value = null

  const trimmedName = questName.value.trim()
  if (trimmedName === '') {
    questError.value = 'Quest name is required.'
    return
  }

  try {
    const result = await createQuestMutation.mutateAsync({
      name: trimmedName,
      writerUserId: optionalContentId(questWriter.value),
    })
    createdQuestId.value = result.id
    questName.value = ''
    questWriter.value = CONTENT_NONE
    toast.success(`Quest created with ID ${result.id}.`)
  } catch (err) {
    questError.value = messageFromContentError(err)
  }
}
</script>

<template>
  <form
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
          <SelectItem :value="CONTENT_NONE">None</SelectItem>
          <SelectItem v-for="writer in writers" :key="writer.id" :value="String(writer.id)">
            {{ writer.name }}
          </SelectItem>
        </SelectContent>
      </Select>
    </div>

    <div
      v-if="questError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ questError }}
    </div>
    <div
      v-if="createdQuestId"
      class="rounded-md border border-emerald-600/30 bg-emerald-50 p-3 text-sm text-emerald-800"
    >
      Quest created with ID {{ createdQuestId }}.
    </div>

    <Button type="submit" :disabled="createQuestMutation.isPending.value || isLoading">
      Create quest
    </Button>
  </form>
</template>
