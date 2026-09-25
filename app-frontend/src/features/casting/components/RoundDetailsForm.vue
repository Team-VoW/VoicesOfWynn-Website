<script setup lang="ts">
import { ref, watch } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { SaveCastingRoundRequest } from '@/api/types'
import { toIso, toLocalInput } from '../castingFormat'

const props = defineProps<{
  initial?: SaveCastingRoundRequest | null
  saving: boolean
  error: string
  submitLabel: string
}>()

const emit = defineEmits<{ submit: [body: SaveCastingRoundRequest]; cancel: [] }>()

const form = ref({ name: '', description: '', closesAt: '' })

watch(
  () => props.initial,
  (initial) => {
    form.value = {
      name: initial?.name ?? '',
      description: initial?.description ?? '',
      closesAt: toLocalInput(initial?.votingClosesAt ?? null),
    }
  },
  { immediate: true },
)

function submit() {
  emit('submit', {
    name: form.value.name.trim(),
    description: form.value.description.trim() || null,
    votingClosesAt: toIso(form.value.closesAt),
  })
}
</script>

<template>
  <form class="space-y-4" @submit.prevent="submit">
    <div class="space-y-2">
      <Label for="round-name">Name</Label>
      <Input id="round-name" v-model="form.name" maxlength="100" placeholder="e.g. Recover the Past, October 2026" />
    </div>
    <div class="space-y-2">
      <Label for="round-description">Description <span class="font-normal text-muted-foreground">(optional)</span></Label>
      <textarea
        id="round-description"
        v-model="form.description"
        rows="2"
        maxlength="2000"
        class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      />
    </div>
    <div class="space-y-2 sm:max-w-[calc(50%-0.5rem)]">
      <Label for="round-closes">Voting closes <span class="font-normal text-muted-foreground">(optional)</span></Label>
      <Input id="round-closes" v-model="form.closesAt" type="datetime-local" />
    </div>

    <div
      v-if="error"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      {{ error }}
    </div>

    <div class="flex gap-2">
      <Button type="submit" :disabled="saving || !form.name.trim()">
        {{ saving ? 'Saving…' : submitLabel }}
      </Button>
      <Button type="button" variant="outline" @click="emit('cancel')">Cancel</Button>
    </div>
  </form>
</template>
