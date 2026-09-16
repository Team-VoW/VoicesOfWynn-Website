<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { ProfileAvatar } from '@/components/ui/profile-avatar'
import type { ContributorSummary } from '@/api/types'

defineProps<{ contributor: ContributorSummary }>()
</script>

<template>
  <RouterLink
    :to="{ name: 'cast', params: { userId: contributor.userId } }"
    class="contributor-card group flex flex-col items-center rounded-2xl border border-[#2a1438]/10 bg-white p-6 text-center shadow-sm transition-[transform,box-shadow] hover:-translate-y-1 hover:shadow-[0_12px_32px_rgba(123,26,155,0.18)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4] motion-reduce:hover:translate-y-0"
  >
    <ProfileAvatar
      :src="contributor.avatarUrl"
      :fallback-src="contributor.defaultAvatarUrl"
      :alt="`${contributor.displayName}'s profile picture`"
      size="md"
    />
    <p class="mt-4 w-full break-words font-display text-base text-[#2a1438]">
      {{ contributor.displayName }}
    </p>
    <p
      v-if="contributor.lore"
      class="mt-2 w-full break-words text-sm italic leading-relaxed text-[#2a1438]/65"
    >
      {{ contributor.lore }}
    </p>
  </RouterLink>
</template>

<style scoped>
/* The gradient rule sweeps in on hover, echoing the legacy credits card without its lift. */
.contributor-card {
  position: relative;
  overflow: hidden;
}

.contributor-card::before {
  content: '';
  position: absolute;
  inset-inline: 0;
  top: 0;
  height: 3px;
  background: linear-gradient(90deg, #7b1a9b, #ff6b9d);
  transform: scaleX(0);
  transform-origin: left;
  transition: transform 0.3s ease;
}

.contributor-card:hover::before,
.contributor-card:focus-visible::before {
  transform: scaleX(1);
}

@media (prefers-reduced-motion: reduce) {
  .contributor-card::before {
    transition: none;
  }
}
</style>
