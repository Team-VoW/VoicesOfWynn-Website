<script setup lang="ts">
import { Edit } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { ContentSearchNpc, ContentSearchQuest } from '@/api/types'
import ReportPagination from '@/features/reports/components/ReportPagination.vue'

defineProps<{
  isError: boolean
  isFetching: boolean
  isLoading: boolean
  page: number
  pageSize: number
  pageSizeOptions: readonly number[]
  searchErrorMessage: string
  searchNpc: string
  searchQuest: string
  searchResults: ContentSearchQuest[]
  total: number
}>()

defineEmits<{
  manageNpc: [quest: ContentSearchQuest, npc: ContentSearchNpc]
  manageQuest: [quest: ContentSearchQuest]
  'update:page': [value: number]
  'update:pageSize': [value: number]
  'update:searchNpc': [value: string]
  'update:searchQuest': [value: string]
}>()
</script>

<template>
  <section class="space-y-5">
    <div class="grid gap-4 rounded-md border bg-background p-5 md:grid-cols-2">
      <div class="space-y-2">
        <Label for="content-search-quest">Quest name</Label>
        <Input
          id="content-search-quest"
          :model-value="searchQuest"
          autocomplete="off"
          placeholder="Search quests"
          @update:model-value="$emit('update:searchQuest', String($event))"
        />
      </div>
      <div class="space-y-2">
        <Label for="content-search-npc">NPC name</Label>
        <Input
          id="content-search-npc"
          :model-value="searchNpc"
          autocomplete="off"
          placeholder="Search NPCs"
          @update:model-value="$emit('update:searchNpc', String($event))"
        />
      </div>
    </div>

    <div
      v-if="isError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      Failed to load content: {{ searchErrorMessage }}
    </div>

    <div class="space-y-3">
      <div
        v-if="isLoading || isFetching"
        class="rounded-md border p-4 text-sm text-muted-foreground"
      >
        Loading content...
      </div>
      <div
        v-else-if="searchResults.length === 0"
        class="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground"
      >
        No quests match the current search.
      </div>

      <article
        v-for="quest in searchResults"
        :key="quest.questId"
        class="rounded-md border bg-background"
      >
        <div class="flex flex-col gap-3 border-b p-4 sm:flex-row sm:items-start sm:justify-between">
          <div class="min-w-0">
            <h2 class="truncate text-base font-medium">{{ quest.questName }}</h2>
            <p class="font-mono text-xs text-muted-foreground">{{ quest.questDegeneratedName }}</p>
          </div>
          <Button variant="outline" size="sm" class="gap-2" @click="$emit('manageQuest', quest)">
            <Edit class="size-4" />
            Manage quest
          </Button>
        </div>

        <div v-if="quest.npcs.length === 0" class="p-4 text-sm text-muted-foreground">
          No NPCs are linked to this quest.
        </div>
        <div v-else class="divide-y">
          <div
            v-for="npc in quest.npcs"
            :key="npc.npcId"
            class="grid gap-3 p-4 text-sm md:grid-cols-[minmax(0,1fr)_minmax(12rem,16rem)_auto] md:items-center"
          >
            <div class="min-w-0">
              <p class="truncate font-medium">{{ npc.npcName }}</p>
              <p class="font-mono text-xs text-muted-foreground">{{ npc.npcDegeneratedName }}</p>
            </div>
            <div class="text-muted-foreground">
              <span class="text-foreground">{{ npc.voiceActorName ?? 'No voice actor' }}</span>
              <p class="text-xs">
                {{ npc.recordingCount }} {{ npc.recordingCount === 1 ? 'recording' : 'recordings' }}
              </p>
            </div>
            <Button
              variant="outline"
              size="sm"
              class="gap-2 justify-self-start md:justify-self-end"
              @click="$emit('manageNpc', quest, npc)"
            >
              <Edit class="size-4" />
              Manage NPC
            </Button>
          </div>
        </div>
      </article>
    </div>

    <ReportPagination
      :page="page"
      :page-size="pageSize"
      :page-size-options="pageSizeOptions"
      :total="total"
      @update:page="$emit('update:page', $event)"
      @update:page-size="$emit('update:pageSize', $event)"
    />
  </section>
</template>
