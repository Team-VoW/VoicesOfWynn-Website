<script setup lang="ts">
import { ProfileAvatar } from '@/components/ui/profile-avatar'
import type { ContributorDetail } from '@/api/types'

defineProps<{ contributor: ContributorDetail }>()

function roleColor(color: string) {
  return /^[0-9a-f]{6}$/i.test(color) ? `#${color}` : '#fbd057'
}
</script>

<template>
  <section class="cast-hero relative overflow-hidden">
    <div
      class="relative z-[1] mx-auto flex max-w-6xl flex-col items-center gap-6 px-4 py-14 text-center sm:px-6 md:py-16"
    >
      <ProfileAvatar
        :src="contributor.avatarUrl"
        :fallback-src="contributor.defaultAvatarUrl"
        :alt="`${contributor.displayName}'s profile picture`"
        size="lg"
        :interactive="false"
      />

      <div>
        <h1 class="font-display text-2xl text-white sm:text-4xl">{{ contributor.displayName }}</h1>
        <p v-if="contributor.lore" class="mt-3 text-lg italic text-white/75">
          {{ contributor.lore }}
        </p>
      </div>

      <ul v-if="contributor.roles.length" class="flex flex-wrap justify-center gap-2">
        <li
          v-for="role in contributor.roles"
          :key="role.id"
          class="rounded-full border-2 px-3 py-1 text-sm"
          :style="{ borderColor: roleColor(role.color), color: roleColor(role.color) }"
        >
          {{ role.name }}
        </li>
      </ul>
    </div>
  </section>
</template>

<style scoped>
.cast-hero {
  background: linear-gradient(160deg, #3b2159 0%, #2e1a47 45%, #1b0f2d 100%);
}

.cast-hero::after {
  content: '';
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(251, 208, 87, 0.55), transparent);
}
</style>
