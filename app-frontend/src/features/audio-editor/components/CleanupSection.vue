<script setup lang="ts">
import { Headphones } from 'lucide-vue-next'

/** One cleanup step: its heading, a Preview toggle to audition it live, and its controls. */
defineProps<{
  title: string
  previewing: boolean
  /** Why the step cannot be previewed yet; disables the toggle and explains on hover. */
  previewBlocked?: string | null
}>()
const emit = defineEmits<{ togglePreview: [] }>()
</script>

<template>
  <section
    class="space-y-2 rounded-md transition-shadow"
    :class="previewing ? '-mx-2 bg-primary/5 px-2 py-2 ring-1 ring-primary/40' : ''"
  >
    <div class="flex items-center justify-between gap-2">
      <h3 class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {{ title }}
      </h3>
      <button
        type="button"
        class="inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium transition-colors disabled:opacity-40"
        :class="
          previewing
            ? 'bg-primary text-primary-foreground'
            : 'text-muted-foreground hover:bg-accent hover:text-foreground'
        "
        :aria-pressed="previewing"
        :disabled="!!previewBlocked"
        :title="
          previewBlocked ||
          (previewing
            ? 'Stop previewing'
            : `Hear ${title.toLowerCase()} while you adjust it, without changing the file`)
        "
        @click="emit('togglePreview')"
      >
        <Headphones class="size-3.5" /> Preview
      </button>
    </div>
    <slot />
  </section>
</template>
