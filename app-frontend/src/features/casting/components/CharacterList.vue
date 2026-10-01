<script setup lang="ts">
import { computed, ref } from 'vue'
import { Check, Star } from 'lucide-vue-next'
import { Input } from '@/components/ui/input'
import type { CastingCharacterSummary } from '@/api/types'
import type { AuditionMark } from '../useAuditionMarks'

const props = defineProps<{
  characters: CastingCharacterSummary[]
  selectedId: number | null
  marks: AuditionMark[]
}>()

const emit = defineEmits<{ select: [id: number] }>()

type Filter = 'all' | 'todo' | 'done' | 'saved'
const filters: { value: Filter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'todo', label: 'To do' },
  { value: 'done', label: 'Done' },
  { value: 'saved', label: 'Saved' },
]

const search = ref('')
const filter = ref<Filter>('all')

const visible = computed(() => {
  const needle = search.value.trim().toLowerCase()
  return props.characters.filter((c) => {
    if (needle && !c.name.toLowerCase().includes(needle)) return false
    if (filter.value === 'todo') return !c.done
    if (filter.value === 'done') return c.done
    if (filter.value === 'saved') return props.marks.some((mark) => mark.characterId === c.id)
    return true
  })
})
</script>

<template>
  <section
    class="flex max-h-80 flex-col overflow-hidden rounded-lg border bg-background lg:sticky lg:top-6 lg:max-h-[calc(100vh-3rem)]"
    aria-label="Characters"
  >
    <div class="space-y-2.5 border-b p-3">
      <Input v-model="search" placeholder="Search characters" aria-label="Search characters" />
      <div class="grid grid-cols-4 gap-1.5" role="group" aria-label="Filter characters">
        <button
          v-for="option in filters"
          :key="option.value"
          type="button"
          class="cursor-pointer rounded-md border py-1.5 text-xs transition-colors"
          :class="
            filter === option.value
              ? 'border-primary/30 bg-primary/10 font-medium text-primary'
              : 'text-muted-foreground hover:bg-muted'
          "
          :aria-pressed="filter === option.value"
          @click="filter = option.value"
        >
          {{ option.label }}
        </button>
      </div>
    </div>

    <ul class="overflow-y-auto p-2">
      <li v-for="character in visible" :key="character.id">
        <button
          type="button"
          class="flex w-full cursor-pointer items-center gap-3 rounded-md px-3 py-2.5 text-left transition-colors"
          :class="
            character.id === selectedId
              ? 'bg-background shadow-sm ring-1 ring-primary/15 [box-shadow:inset_3px_0_0_var(--brand-pink)]'
              : 'hover:bg-muted/70'
          "
          :aria-current="character.id === selectedId ? 'true' : undefined"
          @click="emit('select', character.id)"
        >
          <span
            class="flex size-5 shrink-0 items-center justify-center rounded-full border-[1.5px]"
            :class="
              character.done ? 'border-emerald-600 bg-emerald-600 text-white' : 'border-border'
            "
            :aria-label="character.done ? 'Done' : 'Not done'"
          >
            <Check v-if="character.done" class="size-3" stroke-width="3" />
          </span>
          <span class="min-w-0 flex-1">
            <span
              class="block truncate text-sm"
              :class="character.id === selectedId ? 'font-semibold' : 'font-medium'"
            >
              {{ character.name }}
            </span>
            <span class="block truncate text-xs text-muted-foreground">
              {{ character.auditionCount }} auditions<template v-if="character.questName">
                · {{ character.questName }}</template
              >
            </span>
          </span>
          <span
            v-if="marks.some((mark) => mark.characterId === character.id)"
            class="inline-flex shrink-0 items-center gap-1 text-xs text-primary"
            :aria-label="`${marks.filter((mark) => mark.characterId === character.id).length} saved auditions`"
          >
            <Star class="size-3.5 fill-current" aria-hidden="true" />
            {{ marks.filter((mark) => mark.characterId === character.id).length }}
          </span>
          <span
            v-if="character.myPickCount > 0"
            class="shrink-0 rounded-full bg-primary/10 px-2 py-0.5 text-xs text-primary"
          >
            {{ character.myPickCount }} picked
          </span>
        </button>
      </li>
      <li v-if="visible.length === 0" class="px-3 py-6 text-center text-sm text-muted-foreground">
        No characters match.
      </li>
    </ul>
  </section>
</template>
