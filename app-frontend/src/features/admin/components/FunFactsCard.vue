<script setup lang="ts">
import { computed, ref } from 'vue'
import { toast } from 'vue-sonner'
import { Pencil, Plus, Trash2, X } from 'lucide-vue-next'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { FunFact } from '@/api/types'
import { messageFromContentError } from '@/features/content/contentUtils'
import { useDeleteFunFact, useFunFacts, useSaveFunFact } from '../queries'

const { data, isPending: isLoading } = useFunFacts()
const save = useSaveFunFact()
const remove = useDeleteFunFact()

const editingId = ref<number | null>(null)
const isEditorOpen = ref(false)
const error = ref('')
const form = ref({ slug: '', content: '', active: true })

const isSaving = computed(() => save.isPending.value)
const funFacts = computed(() => data.value?.funFacts ?? [])
const activeCount = computed(() => funFacts.value.filter((fact) => fact.active).length)

function startCreate() {
  editingId.value = null
  form.value = { slug: '', content: '', active: true }
  error.value = ''
  isEditorOpen.value = true
}

function startEdit(fact: FunFact) {
  editingId.value = fact.id
  form.value = { slug: fact.slug, content: fact.content, active: fact.active }
  error.value = ''
  isEditorOpen.value = true
}

async function submit() {
  error.value = ''
  try {
    await save.mutateAsync({
      id: editingId.value,
      body: {
        slug: form.value.slug.trim(),
        content: form.value.content.trim(),
        active: form.value.active,
      },
    })
    toast.success(editingId.value === null ? 'Fun fact added.' : 'Fun fact updated.')
    isEditorOpen.value = false
  } catch (err) {
    error.value = messageFromContentError(err)
  }
}

async function toggle(fact: FunFact) {
  try {
    await save.mutateAsync({
      id: fact.id,
      body: { slug: fact.slug, content: fact.content, active: !fact.active },
    })
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}

async function destroy(fact: FunFact) {
  try {
    await remove.mutateAsync(fact.id)
    toast.success('Fun fact removed.')
  } catch (err) {
    toast.error(messageFromContentError(err))
  }
}
</script>

<template>
  <section class="space-y-5 rounded-md border bg-background p-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="space-y-1">
        <h2 class="text-sm font-semibold">Fun facts</h2>
        <p class="text-sm text-muted-foreground">
          One is picked at random and shown in chat every time a player launches the mod.
          {{ activeCount }} of {{ funFacts.length }} active.
        </p>
      </div>
      <Button type="button" variant="outline" size="sm" @click="startCreate">
        <Plus class="size-4" />
        New fun fact
      </Button>
    </div>

    <form v-if="isEditorOpen" class="space-y-4 rounded-md border p-4" @submit.prevent="submit">
      <div class="space-y-2">
        <Label for="fun-fact-slug">Slug</Label>
        <Input id="fun-fact-slug" v-model="form.slug" placeholder="talking_mushroom_voice" />
        <p class="text-xs text-muted-foreground">
          Lowercase letters, digits and underscores. Identifies the fact; it is never shown.
        </p>
      </div>
      <div class="space-y-2">
        <Label for="fun-fact-content">Text</Label>
        <textarea
          id="fun-fact-content"
          v-model="form.content"
          rows="3"
          maxlength="2000"
          class="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
        />
      </div>
      <label class="flex items-center gap-2 text-sm">
        <input v-model="form.active" type="checkbox" class="size-4" />
        Active
      </label>

      <div
        v-if="error"
        class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
      >
        {{ error }}
      </div>

      <div class="flex gap-2">
        <Button type="submit" :disabled="isSaving || !form.slug.trim() || !form.content.trim()">
          {{ isSaving ? 'Saving…' : editingId === null ? 'Add' : 'Save changes' }}
        </Button>
        <Button type="button" variant="outline" @click="isEditorOpen = false">
          <X class="size-4" />
          Cancel
        </Button>
      </div>
    </form>

    <p v-if="isLoading" class="text-sm text-muted-foreground">Loading…</p>

    <ul v-else class="space-y-2">
      <li
        v-for="fact in funFacts"
        :key="fact.id"
        class="flex flex-wrap items-start gap-3 rounded-md border p-3 text-sm"
      >
        <div class="min-w-0 flex-1 space-y-1">
          <p class="break-words" :class="{ 'text-muted-foreground': !fact.active }">
            {{ fact.content }}
          </p>
          <p class="font-mono text-xs text-muted-foreground">{{ fact.slug }}</p>
        </div>
        <Badge
          :variant="fact.active ? 'secondary' : 'outline'"
          class="cursor-pointer"
          @click="toggle(fact)"
        >
          {{ fact.active ? 'Active' : 'Hidden' }}
        </Badge>
        <div class="flex gap-1">
          <Button type="button" variant="ghost" size="icon-sm" @click="startEdit(fact)">
            <Pencil class="size-4" />
          </Button>
          <Button type="button" variant="ghost" size="icon-sm" @click="destroy(fact)">
            <Trash2 class="size-4" />
          </Button>
        </div>
      </li>
    </ul>
  </section>
</template>
