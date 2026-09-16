<script setup lang="ts">
import { computed } from 'vue'
import type { Component } from 'vue'
import { Github, Instagram, Mail } from 'lucide-vue-next'
import type { ContributorDetail } from '@/api/types'

const props = defineProps<{ contributor: ContributorDetail }>()

interface SocialLink {
  key: string
  label: string
  href: string
  /** Either a bundled brand image or, where no image asset exists, a lucide icon component. */
  icon?: string
  iconComponent?: Component
  classes: string
}

const links = computed<SocialLink[]>(() => {
  const result: SocialLink[] = []
  const { youtube, twitter, instagram, github, castingCallClub } = props.contributor

  // YouTube values are stored as full URLs while every other handle is bare, which is why only
  // the others get a profile-URL prefix and only YouTube shows a fixed label.
  if (youtube) {
    result.push({
      key: 'youtube',
      label: 'YouTube',
      href: youtube,
      icon: '/images/youtube_icon.png',
      classes: 'bg-[#FF3D00] hover:bg-[#ff5722] text-white',
    })
  }
  if (twitter) {
    result.push({
      key: 'twitter',
      label: twitter,
      href: `https://x.com/${twitter}`,
      icon: '/images/x_icon.svg',
      classes: 'bg-black hover:bg-[#1a1a1a] text-white',
    })
  }
  if (instagram) {
    result.push({
      key: 'instagram',
      label: instagram,
      href: `https://instagram.com/${instagram}`,
      iconComponent: Instagram,
      classes: 'bg-[#C13584] hover:bg-[#d8438f] text-white',
    })
  }
  if (github) {
    result.push({
      key: 'github',
      label: github,
      href: `https://github.com/${github}`,
      iconComponent: Github,
      classes: 'bg-[#24292F] hover:bg-[#3a4149] text-white',
    })
  }
  if (castingCallClub) {
    result.push({
      key: 'castingcallclub',
      label: castingCallClub,
      href: `https://castingcall.club/${castingCallClub}`,
      icon: '/images/castingcallclub_icon.png',
      classes: 'bg-[#6827F7] hover:bg-[#7c3aed] text-white',
    })
  }
  return result
})

const hasAnything = computed(
  () => links.value.length > 0 || !!props.contributor.discord || !!props.contributor.email,
)
</script>

<template>
  <div v-if="hasAnything" class="flex flex-wrap items-center justify-center gap-3">
    <span
      v-if="contributor.discord"
      class="flex items-center gap-2 rounded-xl border border-[#2a1438]/10 bg-white px-4 py-2.5 text-[#2a1438] shadow-sm"
    >
      <img src="/images/discord.png" alt="" class="size-5 rounded-sm" />
      {{ contributor.discord }}
    </span>

    <a
      v-for="link in links"
      :key="link.key"
      :href="link.href"
      target="_blank"
      rel="noopener noreferrer"
      class="flex items-center gap-2 rounded-xl px-4 py-2.5 shadow-sm transition-[transform,background-color] hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4] motion-reduce:hover:translate-y-0"
      :class="link.classes"
    >
      <component
        :is="link.iconComponent"
        v-if="link.iconComponent"
        class="size-5"
        aria-hidden="true"
      />
      <img v-else :src="link.icon" alt="" class="size-5 rounded-sm" />
      {{ link.label }}
    </a>

    <a
      v-if="contributor.email"
      :href="`mailto:${contributor.email}`"
      class="flex items-center gap-2 rounded-xl bg-[#ffc107] px-4 py-2.5 text-[#2a1438] shadow-sm transition-[transform,background-color] hover:-translate-y-0.5 hover:bg-[#ffd54f] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#a340c4] motion-reduce:hover:translate-y-0"
    >
      <Mail class="size-5" aria-hidden="true" />
      Contact
    </a>
  </div>
</template>
