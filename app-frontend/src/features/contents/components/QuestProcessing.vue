<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { AudioLines, FileText } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import type { ContentCredit, QuestDetail, QuestNpc } from '@/api/types'

const props = defineProps<{ quest: QuestDetail }>()

interface EditorGroup {
  editor: ContentCredit
  npcs: QuestNpc[]
}

// One card per editor with their NPCs as chips. The legacy page listed the editor again for
// every NPC they had worked on.
const editors = computed(() => {
  const groups = new Map<number, EditorGroup>()
  for (const npc of props.quest.npcs) {
    const editor = npc.soundEditor
    if (!editor) continue
    const group = groups.get(editor.userId)
    if (group) group.npcs.push(npc)
    else groups.set(editor.userId, { editor, npcs: [npc] })
  }
  return [...groups.values()]
})

const hasAnything = computed(
  () => props.quest.writer !== null || props.quest.scriptUrl !== null || editors.value.length > 0,
)

function onAvatarError(event: Event, credit: ContentCredit) {
  const image = event.target as HTMLImageElement
  if (image.src !== credit.defaultAvatarUrl) image.src = credit.defaultAvatarUrl
}
</script>

<template>
  <section v-if="hasAnything">
    <h2 class="font-display text-xl text-[#2a1438] sm:text-2xl">Processing</h2>

    <div
      v-if="quest.writer || quest.scriptUrl"
      class="mt-6 flex flex-wrap items-center gap-x-4 gap-y-3 rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm"
    >
      <template v-if="quest.writer">
        <RouterLink :to="`/cast/${quest.writer.userId}`" class="shrink-0">
          <img
            :src="quest.writer.avatarUrl"
            :alt="`${quest.writer.displayName}'s profile picture`"
            loading="lazy"
            class="size-12 rounded-full bg-[#faf6fd] object-cover"
            @error="onAvatarError($event, quest.writer)"
          />
        </RouterLink>
        <div class="min-w-0 flex-1 basis-40">
          <p class="text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Script by</p>
          <p class="mt-1 font-display text-base text-[#2a1438]">
            <RouterLink
              :to="`/cast/${quest.writer.userId}`"
              class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
            >
              {{ quest.writer.displayName }}
            </RouterLink>
          </p>
        </div>
      </template>
      <p v-else class="min-w-0 flex-1 basis-40 text-sm text-[#2a1438]/65">
        Nobody is credited for this script yet.
      </p>

      <Button v-if="quest.scriptUrl" as-child variant="brand" size="sm" class="shrink-0">
        <a :href="quest.scriptUrl" target="_blank" rel="noopener noreferrer">
          <FileText class="size-4" aria-hidden="true" />
          View script
        </a>
      </Button>
    </div>

    <template v-if="editors.length">
      <h3 class="mt-8 text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Sound editing</h3>
      <ul class="mt-4 grid gap-4 md:grid-cols-2">
        <li
          v-for="group in editors"
          :key="group.editor.userId"
          class="rounded-2xl border border-[#2a1438]/10 bg-white p-5 shadow-sm"
        >
          <div class="flex items-center gap-4">
            <span
              class="flex size-11 shrink-0 items-center justify-center rounded-lg bg-[#fbd057]/25 text-[#7b1a9b]"
            >
              <AudioLines class="size-5" aria-hidden="true" />
            </span>
            <div class="min-w-0">
              <p class="text-xs uppercase tracking-[0.2em] text-[#7b1a9b]">Edited by</p>
              <p class="mt-1 font-display text-base text-[#2a1438]">
                <RouterLink
                  :to="`/cast/${group.editor.userId}`"
                  class="underline-offset-4 hover:text-[#7b1a9b] hover:underline"
                >
                  {{ group.editor.displayName }}
                </RouterLink>
              </p>
            </div>
          </div>

          <ul class="mt-4 flex flex-wrap gap-2">
            <li v-for="npc in group.npcs" :key="npc.npcId">
              <RouterLink
                :to="`/contents/npc/${npc.npcId}`"
                class="inline-flex items-center rounded-full bg-[#a340c4]/10 px-3 py-1.5 text-sm text-[#7b1a9b] transition-colors hover:bg-[#a340c4]/20 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4]"
              >
                {{ npc.npcName }}
              </RouterLink>
            </li>
          </ul>
        </li>
      </ul>
    </template>
  </section>
</template>
