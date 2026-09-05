<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { apiFetch } from '@/api/client'
import { Button } from '@/components/ui/button'
import type { Page, QuestSummary, QuestDetail } from './types'

const filters = reactive({ search: '', from: '', to: '', minimumCount: 1, sort: 'lowest' })
const applied = ref({ ...filters })
const page = ref(1)
const selected = ref<QuestSummary | null>(null)
const detailPage = ref(1)
const commentsOnly = ref(false)
const invalid = computed(
  () =>
    filters.minimumCount < 1 ||
    filters.minimumCount > 1000000 ||
    !Number.isInteger(filters.minimumCount) ||
    (filters.from && filters.to && filters.from > filters.to),
)
const summary = useQuery({
  queryKey: computed(() => ['quest-feedback', applied.value, page.value]),
  queryFn: ({ signal }) =>
    apiFetch<Page<QuestSummary>>('/admin/feedback/quests', {
      query: { ...applied.value, page: page.value },
      signal,
    }),
})
const detail = useQuery({
  queryKey: computed(() => [
    'quest-feedback-detail',
    selected.value?.groupingKey,
    applied.value.from,
    applied.value.to,
    detailPage.value,
    commentsOnly.value,
  ]),
  enabled: computed(() => selected.value !== null),
  queryFn: ({ signal }) =>
    apiFetch<QuestDetail>('/admin/feedback/quests/detail', {
      query: {
        key: selected.value?.groupingKey,
        from: applied.value.from,
        to: applied.value.to,
        page: detailPage.value,
        commentsOnly: commentsOnly.value,
      },
      signal,
    }),
})
watch(commentsOnly, () => {
  detailPage.value = 1
})
function apply() {
  if (invalid.value) return
  applied.value = { ...filters }
  page.value = 1
  selected.value = null
}
function select(quest: QuestSummary) {
  selected.value = quest
  detailPage.value = 1
  commentsOnly.value = false
}
function date(value: string) {
  return new Date(value).toLocaleString()
}
const sampleSize = computed(
  () => detail.data.value?.distribution.reduce((sum, item) => sum + item.count, 0) ?? 0,
)
</script>

<template>
  <div class="space-y-6">
    <h1 class="text-2xl font-semibold">Quest Feedback</h1>
    <form class="flex flex-wrap items-end gap-4 rounded-lg border p-4" @submit.prevent="apply">
      <label class="grid gap-1 text-sm"
        >Quest search<input
          v-model="filters.search"
          maxlength="200"
          class="rounded border bg-background p-2"
          type="search"
      /></label>
      <label class="grid gap-1 text-sm"
        >From<input v-model="filters.from" class="rounded border bg-background p-2" type="date"
      /></label>
      <label class="grid gap-1 text-sm"
        >Through<input v-model="filters.to" class="rounded border bg-background p-2" type="date"
      /></label>
      <label class="grid gap-1 text-sm"
        >Minimum ratings<input
          v-model.number="filters.minimumCount"
          class="w-28 rounded border bg-background p-2"
          type="number"
          min="1"
          max="1000000"
          required
      /></label>
      <label class="grid gap-1 text-sm"
        >Sort<select v-model="filters.sort" class="rounded border bg-background p-2">
          <option value="lowest">Lowest average first</option>
          <option value="highest">Highest average first</option>
        </select></label
      >
      <Button type="submit" :disabled="!!invalid">Apply filters</Button>
      <p v-if="invalid" role="alert">Enter a valid date range and minimum rating count.</p>
    </form>
    <p v-if="summary.isPending.value" role="status">Loading quest feedback…</p>
    <div v-else-if="summary.isError.value" role="alert">
      Could not load quest feedback.
      <Button variant="outline" @click="summary.refetch()">Retry</Button>
    </div>
    <template v-else-if="summary.data.value">
      <p v-if="!summary.data.value.items.length">No ratings match these filters.</p>
      <div v-else class="overflow-x-auto rounded-lg border">
        <table class="w-full text-left text-sm">
          <thead class="bg-muted">
            <tr>
              <th>Quest</th>
              <th>Average / 5</th>
              <th>Ratings (sample size)</th>
              <th>Comments</th>
              <th>Latest submission</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="quest in summary.data.value.items" :key="quest.groupingKey" class="border-t">
              <td>
                <button class="text-left underline" @click="select(quest)">
                  {{ quest.questName }}</button
                ><span v-if="quest.unmatched" class="ml-2 text-muted-foreground">Unmatched</span>
                <span v-else-if="quest.unmatchedCount" class="ml-2 text-muted-foreground"
                  >{{ quest.unmatchedCount }} unmatched submissions</span
                >
              </td>
              <td>{{ quest.averageScore.toFixed(2) }} / 5</td>
              <td>{{ quest.ratingCount }}</td>
              <td>{{ quest.commentCount }}</td>
              <td>{{ date(quest.latestSubmission) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex items-center gap-3">
        <Button variant="outline" :disabled="page === 1" @click="page--">Previous</Button
        ><span>Page {{ page }} · {{ summary.data.value.total }} quests</span
        ><Button
          variant="outline"
          :disabled="page * summary.data.value.pageSize >= summary.data.value.total"
          @click="page++"
          >Next</Button
        >
      </div>
    </template>
    <section
      v-if="selected"
      class="space-y-4 rounded-lg border p-4"
      aria-label="Quest feedback details"
    >
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold">{{ selected.questName }}</h2>
        <Button variant="ghost" @click="selected = null">Close details</Button>
      </div>
      <label class="flex gap-2"
        ><input v-model="commentsOnly" type="checkbox" />Comments only</label
      >
      <p v-if="detail.isPending.value" role="status">Loading feedback…</p>
      <div v-else-if="detail.isError.value" role="alert">
        Could not load feedback. <Button variant="outline" @click="detail.refetch()">Retry</Button>
      </div>
      <template v-else-if="detail.data.value">
        <p>Score distribution · {{ sampleSize }} ratings in the selected date range</p>
        <ul class="flex flex-wrap gap-5">
          <li v-for="score in 5" :key="score">
            {{ score }} / 5:
            {{ detail.data.value.distribution.find((item) => item.score === score)?.count ?? 0 }}
          </li>
        </ul>
        <p v-if="!detail.data.value.feedback.items.length">
          No {{ commentsOnly ? 'written feedback' : 'ratings' }} match these filters.
        </p>
        <article
          v-for="(item, index) in detail.data.value.feedback.items"
          :key="index"
          class="space-y-2 border-t py-3"
        >
          <p>
            <strong>{{ item.score }} / 5</strong> · {{ date(item.createdAt) }} · Mod
            {{ item.modVersion }}
          </p>
          <p v-if="item.unmatched" class="text-muted-foreground">Unmatched: {{ item.questName }}</p>
          <p v-if="item.comment" class="whitespace-pre-wrap break-words">{{ item.comment }}</p>
          <p v-else class="text-muted-foreground">No written feedback</p>
        </article>
        <div class="flex items-center gap-3">
          <Button variant="outline" :disabled="detailPage === 1" @click="detailPage--"
            >Previous feedback</Button
          ><span>Page {{ detailPage }} · {{ detail.data.value.feedback.total }} submissions</span
          ><Button
            variant="outline"
            :disabled="
              detailPage * detail.data.value.feedback.pageSize >= detail.data.value.feedback.total
            "
            @click="detailPage++"
            >Next feedback</Button
          >
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
th,
td {
  padding: 0.75rem;
}
</style>
