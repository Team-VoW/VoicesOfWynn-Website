<script setup lang="ts">
import { computed } from 'vue'
import InfiniteScrollSentinel from '@/components/InfiniteScrollSentinel.vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import type { ContributorRole, ContributorSummary } from '@/api/types'
import RoleSection from '../components/RoleSection.vue'
import { CONTRIBUTORS_PAGE_SIZE, useContributors } from '../queries'

const { data, error, isPending, isError, fetchNextPage, hasNextPage, isFetchingNextPage, refetch } =
  useContributors()

const contributors = computed(() => data.value?.pages.flatMap((page) => page.results) ?? [])
const total = computed(() => data.value?.pages[0]?.total ?? 0)

interface RoleGroup {
  role: ContributorRole
  contributors: ContributorSummary[]
}

// The API orders contributors by their top role, so a role's members always arrive together and
// a group can be closed as soon as the role changes - even across page boundaries.
const groups = computed<RoleGroup[]>(() => {
  const result: RoleGroup[] = []
  for (const contributor of contributors.value) {
    const last = result[result.length - 1]
    if (last && last.role.id === contributor.topRole.id) last.contributors.push(contributor)
    else result.push({ role: contributor.topRole, contributors: [contributor] })
  }
  return result
})
</script>

<template>
  <section class="border-b border-[#a340c4]/15 bg-[#faf6fd]">
    <div class="mx-auto max-w-6xl px-4 py-14 text-center sm:px-6 md:py-16">
      <p
        class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]"
      >
        <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
        The people behind the voices
      </p>
      <h1 class="mt-4 font-display text-2xl text-[#2a1438] sm:text-3xl">Contributors</h1>
      <p class="mx-auto mt-4 max-w-2xl text-lg leading-relaxed text-[#2a1438]/75">
        A lot of people worked hard on this mod. Take a moment to appreciate their work.
      </p>
    </div>
  </section>

  <section class="bg-white">
    <div class="mx-auto max-w-6xl px-4 py-14 sm:px-6 md:py-16">
      <div v-if="isPending" class="flex flex-wrap justify-center gap-5">
        <div
          v-for="index in CONTRIBUTORS_PAGE_SIZE"
          :key="index"
          class="flex w-[calc(50%-0.625rem)] flex-col items-center rounded-2xl border border-[#2a1438]/10 bg-white p-6 sm:w-[13.75rem]"
        >
          <Skeleton class="size-20 rounded-full" />
          <Skeleton class="mt-4 h-4 w-24" />
        </div>
      </div>

      <div
        v-else-if="isError"
        class="rounded-2xl border border-destructive/30 bg-destructive/5 p-8 text-center"
      >
        <p class="text-[#2a1438]">The contributors could not be loaded.</p>
        <p class="mt-2 text-sm text-[#2a1438]/65">{{ error?.message }}</p>
        <Button variant="outline" class="mt-6" @click="refetch()">Try again</Button>
      </div>

      <template v-else>
        <RoleSection
          v-for="group in groups"
          :key="group.role.id"
          :role="group.role"
          :contributors="group.contributors"
        />

        <!-- Infinite scroll is silent, so the count is announced as pages arrive. -->
        <p aria-live="polite" class="sr-only">
          Showing {{ contributors.length }} of {{ total }} contributors
        </p>

        <div v-if="isFetchingNextPage" class="mt-10 flex justify-center">
          <p class="text-sm text-[#2a1438]/60">Loading more contributors…</p>
        </div>

        <InfiniteScrollSentinel
          :disabled="!hasNextPage || isFetchingNextPage"
          @load="fetchNextPage()"
        />

        <!-- A manual control keeps the list reachable without a scroll gesture. -->
        <div v-if="hasNextPage && !isFetchingNextPage" class="mt-10 flex justify-center">
          <Button variant="outline" @click="fetchNextPage()">Show more contributors</Button>
        </div>
      </template>
    </div>
  </section>
</template>
