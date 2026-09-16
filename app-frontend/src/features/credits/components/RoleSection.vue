<script setup lang="ts">
import { computed } from 'vue'
import type { ContributorRole, ContributorSummary } from '@/api/types'
import ContributorCard from './ContributorCard.vue'

const props = defineProps<{ role: ContributorRole; contributors: ContributorSummary[] }>()

// Roles whose last word is already collective, so a trailing "s" would read wrong
// ("Former Staffs"). Everything else takes a plain "s".
const COLLECTIVE_NOUNS = new Set(['staff', 'crew', 'team'])

// The legacy page appended "s" to every role except the one that happened to be held by a single
// person. Pluralizing on the group's actual size says the same thing without naming a role.
const heading = computed(() => {
  const { name } = props.role
  if (props.contributors.length === 1) return name
  const lastWord = name.split(' ').pop()?.toLowerCase() ?? ''
  return COLLECTIVE_NOUNS.has(lastWord) ? name : `${name}s`
})

// Role colours come from Discord and are only validated as six hex digits there, so anything
// else falls back to the brand purple rather than producing a broken style attribute.
const color = computed(() =>
  /^[0-9a-f]{6}$/i.test(props.role.color) ? `#${props.role.color}` : '#7b1a9b',
)
</script>

<template>
  <section class="mt-16 text-center first:mt-0">
    <h2 class="font-display text-2xl sm:text-[1.75rem]" :style="{ color }">{{ heading }}</h2>
    <span
      class="mx-auto mt-6 block h-[3px] w-20 rounded-full bg-[linear-gradient(90deg,#7b1a9b,#ff6b9d)]"
    />

    <!-- Centred wrapping row rather than a grid: a role with two members should sit in the middle
         of the page, not tucked against the left edge of a five-column track. -->
    <ul class="mt-8 flex flex-wrap justify-center gap-5">
      <li
        v-for="contributor in contributors"
        :key="contributor.userId"
        class="flex w-[calc(50%-0.625rem)] sm:w-[13.75rem]"
      >
        <ContributorCard :contributor="contributor" class="w-full" />
      </li>
    </ul>
  </section>
</template>
