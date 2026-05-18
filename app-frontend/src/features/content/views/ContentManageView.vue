<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { refDebounced } from '@vueuse/core'
import type { ContentSearchNpc, ContentSearchQuest, ContentSearchRequest } from '@/api/types'
import ContentCreateNpcForm from '../components/ContentCreateNpcForm.vue'
import ContentCreateQuestForm from '../components/ContentCreateQuestForm.vue'
import ContentMassUpload from '../components/ContentMassUpload.vue'
import ContentManageDialog from '../components/ContentManageDialog.vue'
import ContentManageSearch from '../components/ContentManageSearch.vue'
import { messageFromContentError } from '../contentUtils'
import { useContentOptions, useContentSearch } from '../queries'

type Tab = 'quest' | 'npc' | 'manage' | 'mass-upload'
type DialogMode = 'quest' | 'npc' | null

const DEFAULT_PAGE_SIZE = 25
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const

const route = useRoute()
const router = useRouter()

function stringFromQuery(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function pageSizeFromQuery(value: unknown): number {
  const pageSize = Number(value)
  return PAGE_SIZE_OPTIONS.includes(pageSize as (typeof PAGE_SIZE_OPTIONS)[number])
    ? pageSize
    : DEFAULT_PAGE_SIZE
}

function tabFromQuery(value: unknown): Tab {
  if (value === 'manage' || value === 'npc' || value === 'mass-upload') return value
  return 'quest'
}

const activeTab = ref<Tab>(tabFromQuery(route.query.tab))
const searchQuest = ref(stringFromQuery(route.query.quest))
const searchNpc = ref(stringFromQuery(route.query.npc))
const page = ref(Number(route.query.page) > 0 ? Number(route.query.page) : 1)
const pageSize = ref(pageSizeFromQuery(route.query.pageSize))
const searchQuestDebounced = refDebounced(searchQuest, 300)
const searchNpcDebounced = refDebounced(searchNpc, 300)

const dialogOpen = ref(false)
const dialogMode = ref<DialogMode>(null)
const selectedQuest = ref<ContentSearchQuest | null>(null)
const selectedNpc = ref<ContentSearchNpc | null>(null)
const pendingReplacementNpcId = ref<number | null>(null)

const { data: options, isLoading, isError, error } = useContentOptions()

const quests = computed(() => options.value?.quests ?? [])
const npcs = computed(() => options.value?.npcs ?? [])
const writers = computed(() => options.value?.writers ?? [])
const voiceActors = computed(() => options.value?.voiceActors ?? [])
const soundEditors = computed(() => options.value?.soundEditors ?? [])

watch([searchQuestDebounced, searchNpcDebounced], () => {
  page.value = 1
})

watch(pageSize, () => {
  page.value = 1
})

const searchParams = computed<ContentSearchRequest>(() => ({
  quest: searchQuestDebounced.value || undefined,
  npc: searchNpcDebounced.value || undefined,
  page: page.value,
  pageSize: pageSize.value,
}))

const {
  data: searchData,
  isLoading: searchLoading,
  isFetching: searchFetching,
  isError: searchIsError,
  error: searchError,
  refetch: refetchContentSearch,
} = useContentSearch(searchParams)

const searchResults = computed(() => searchData.value?.results ?? [])
const total = computed(() => searchData.value?.total ?? 0)

watch(
  [activeTab, searchParams],
  () => {
    void router.replace({
      query: {
        ...(activeTab.value !== 'quest' ? { tab: activeTab.value } : {}),
        ...(activeTab.value === 'manage' && searchParams.value.quest
          ? { quest: searchParams.value.quest }
          : {}),
        ...(activeTab.value === 'manage' && searchParams.value.npc
          ? { npc: searchParams.value.npc }
          : {}),
        ...(activeTab.value === 'manage' && searchParams.value.page > 1
          ? { page: String(searchParams.value.page) }
          : {}),
        ...(activeTab.value === 'manage' && searchParams.value.pageSize !== DEFAULT_PAGE_SIZE
          ? { pageSize: String(searchParams.value.pageSize) }
          : {}),
      },
    })
  },
  { immediate: false },
)

watch(
  () => route.query,
  (q) => {
    const nextTab = tabFromQuery(q.tab)
    const nextQuest = stringFromQuery(q.quest)
    const nextNpc = stringFromQuery(q.npc)
    const nextPage = Number(q.page) > 0 ? Number(q.page) : 1
    const nextPageSize = pageSizeFromQuery(q.pageSize)

    if (nextTab !== activeTab.value) activeTab.value = nextTab
    if (nextQuest !== searchQuest.value) searchQuest.value = nextQuest
    if (nextNpc !== searchNpc.value) searchNpc.value = nextNpc
    if (nextPage !== page.value) page.value = nextPage
    if (nextPageSize !== pageSize.value) pageSize.value = nextPageSize
  },
)

function openQuestDialog(quest: ContentSearchQuest) {
  selectedQuest.value = quest
  selectedNpc.value = null
  dialogMode.value = 'quest'
  dialogOpen.value = true
}

function openNpcDialog(quest: ContentSearchQuest, npc: ContentSearchNpc) {
  selectedQuest.value = quest
  selectedNpc.value = npc
  dialogMode.value = 'npc'
  dialogOpen.value = true
}

function findNpcInSearchResults(npcId: number) {
  for (const quest of searchResults.value) {
    const npc = quest.npcs.find((candidate) => candidate.npcId === npcId)
    if (npc) return { quest, npc }
  }

  return null
}

function openReplacementNpcIfAvailable(npcId: number) {
  const match = findNpcInSearchResults(npcId)
  if (!match) return false

  openNpcDialog(match.quest, match.npc)
  pendingReplacementNpcId.value = null
  return true
}

async function onNpcArchived(replacementNpcId: number | null) {
  if (replacementNpcId === null) {
    selectedQuest.value = null
    selectedNpc.value = null
    dialogMode.value = null
    return
  }

  pendingReplacementNpcId.value = replacementNpcId
  dialogOpen.value = false
  await refetchContentSearch()
  openReplacementNpcIfAvailable(replacementNpcId)
}

watch(searchResults, () => {
  if (pendingReplacementNpcId.value !== null) {
    openReplacementNpcIfAvailable(pendingReplacementNpcId.value)
  }
})
</script>

<template>
  <div class="mx-auto max-w-screen-xl space-y-6">
    <header class="space-y-1">
      <h1 class="text-xl font-semibold tracking-tight">Manage content</h1>
      <p class="text-sm text-muted-foreground">
        Create and maintain quests and NPCs for website content.
      </p>
    </header>

    <div class="inline-flex flex-wrap rounded-md border bg-background p-1">
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
      <button
        type="button"
        class="rounded-sm px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors"
        :class="{ 'bg-accent text-accent-foreground': activeTab === 'manage' }"
        @click="activeTab = 'manage'"
      >
        Manage all
      </button>
      <button
        type="button"
        class="rounded-sm px-3 py-1.5 text-sm font-medium text-muted-foreground transition-colors"
        :class="{ 'bg-accent text-accent-foreground': activeTab === 'mass-upload' }"
        @click="activeTab = 'mass-upload'"
      >
        Mass upload
      </button>
    </div>

    <div
      v-if="isError"
      class="rounded-md border border-destructive/50 bg-destructive/5 p-3 text-sm text-destructive"
    >
      Failed to load content options: {{ messageFromContentError(error) }}
    </div>

    <ContentCreateQuestForm
      v-if="activeTab === 'quest'"
      :is-loading="isLoading"
      :writers="writers"
    />

    <ContentCreateNpcForm
      v-else-if="activeTab === 'npc'"
      :is-loading="isLoading"
      :quests="quests"
      :sound-editors="soundEditors"
      :voice-actors="voiceActors"
    />

    <ContentMassUpload
      v-else-if="activeTab === 'mass-upload'"
      :is-loading="isLoading"
      :npcs="npcs"
      :quests="quests"
    />

    <ContentManageSearch
      v-else
      v-model:page="page"
      v-model:page-size="pageSize"
      v-model:search-npc="searchNpc"
      v-model:search-quest="searchQuest"
      :is-error="searchIsError"
      :is-fetching="searchFetching"
      :is-loading="searchLoading"
      :page-size-options="PAGE_SIZE_OPTIONS"
      :search-error-message="messageFromContentError(searchError)"
      :search-results="searchResults"
      :total="total"
      @manage-npc="openNpcDialog"
      @manage-quest="openQuestDialog"
    />

    <ContentManageDialog
      v-model:open="dialogOpen"
      :is-loading="isLoading"
      :mode="dialogMode"
      :npcs="npcs"
      :selected-npc="selectedNpc"
      :selected-quest="selectedQuest"
      :sound-editors="soundEditors"
      :voice-actors="voiceActors"
      :writers="writers"
      @npc-archived="onNpcArchived"
    />
  </div>
</template>
