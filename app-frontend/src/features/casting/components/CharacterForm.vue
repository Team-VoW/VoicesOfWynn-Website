<script setup lang="ts">
import { ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { SaveCastingCharacterRequest } from '@/api/types'

const props = defineProps<{
  initial?: SaveCastingCharacterRequest | null
  saving: boolean
  error: string
  submitLabel: string
}>()

const emit = defineEmits<{ submit: [body: SaveCastingCharacterRequest]; cancel: [] }>()

const form = ref({ name: '', questName: '', direction: '' })

watch(
  () => props.initial,
  (initial) => {
    form.value = {
      name: initial?.name ?? '',
      questName: initial?.questName ?? '',
      direction: initial?.direction ?? '',
    }
  },
  { immediate: true },
)

function submit() {
  emit('submit', {
    name: form.value.name.trim(),
    questName: form.value.questName.trim() || null,
    direction: form.value.direction.trim() || null,
  })
}
</script>

<template>
  <form class="space-y-3" @submit.prevent="submit">
    <div class="grid gap-3 sm:grid-cols-2">
      <div class="space-y-1.5">
        <Label for="character-name">Character</Label>
        <Input id="character-name" v-model="form.name" maxlength="100" />
      </div>
      <div class="space-y-1.5">
        <Label for="character-quest">Quest <span class="font-normal text-muted-foreground">(optional)</span></Label>
        <Input id="character-quest" v-model="form.questName" maxlength="100" />
      </div>
    </div>
    <div class="space-y-1.5">
      <Label for="character-direction">Direction <span class="font-normal text-muted-foreground">(optional)</span></Label>
      <textarea
        id="character-direction"
        v-model="form.direction"
        rows="2"
        maxlength="2000"
        placeholder="e.g. Weary knight, late 50s. Gravelly, slow and tired but warm underneath."
        class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      />
    </div>
    <div
      v-if="error"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ error }}
    </div>
    <div class="flex gap-2">
      <Button type="submit" size="sm" :disabled="saving || !form.name.trim()">
        {{ saving ? 'Saving…' : submitLabel }}
      </Button>
      <Button type="button" size="sm" variant="outline" @click="emit('cancel')">Cancel</Button>
    </div>
  </form>
</template>
