<script setup lang="ts">
import { CircleHelp, Download, Heart, LogIn, Package, User, Users } from 'lucide-vue-next'
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import VideoEmbed from '../components/VideoEmbed.vue'
import VoiceMark from '../components/VoiceMark.vue'

const auth = useAuthStore()

const MODRINTH_URL = 'https://modrinth.com/mod/vow'
const DISCORD_URL = 'https://discord.gg/kuEK3XH4Y5'
const PATREON_URL = 'https://www.patreon.com/Voices_Of_Wynn'

const readyPages = [
  {
    title: 'Credits',
    icon: Users,
    description:
      'A lot of great people have put tons of effort into the Voices of Wynn mod. Take a minute to appreciate their work!',
    action: 'Credits',
    to: { name: 'credits' } as const,
  },
  {
    title: "What's in the box?",
    icon: Package,
    description:
      'Do you want to know who your favorite NPCs were voiced by? Want to listen to specific voice lines?',
    action: 'Mod contents',
    to: { name: 'contents' } as const,
  },
  {
    title: 'FAQ',
    icon: CircleHelp,
    description: 'Do you have any unanswered questions? Check out the FAQ!',
    action: 'FAQ',
    to: { name: 'faq' } as const,
  },
]
</script>

<template>
  <!-- Hero -->
  <section class="hero relative overflow-hidden">
    <div
      class="hero-content mx-auto grid max-w-6xl items-center gap-10 px-4 py-14 sm:px-6 md:py-20 lg:grid-cols-[1.05fr_1fr] lg:gap-14"
    >
      <div class="text-white">
        <!-- font-display trims its leading, so the heading box is exactly the
             glyphs: the crest hangs out of flow, centred on them. -->
        <h1 class="relative pl-[1.5em] font-display text-2xl sm:text-4xl lg:text-5xl">
          <img
            src="/wynnvplogo.svg"
            alt=""
            class="absolute left-0 top-[-0.15em] h-[1.3em] w-auto drop-shadow"
          />
          Voices of Wynn
        </h1>

        <p class="mt-5 max-w-xl text-lg leading-relaxed text-white/85">
          Do you want a more immersive Wynncraft experience? Are you tired of having to read
          dialogue while doing quests? Then Voices of Wynn is the mod for you! It adds voice acting
          for all 130+ quests in Wynncraft and more!
        </p>

        <div class="mt-8 flex flex-wrap items-center gap-3">
          <Button as-child variant="brand" size="lg">
            <a :href="MODRINTH_URL" target="_blank" rel="noopener noreferrer">
              <Download class="size-4" aria-hidden="true" />
              Download the mod
            </a>
          </Button>
          <Button
            as-child
            size="lg"
            class="border border-white/35 bg-white/10 text-white hover:bg-white/20"
          >
            <a :href="DISCORD_URL" target="_blank" rel="noopener">
              <img src="/images/discord.png" alt="" class="size-5 rounded-sm" />
              Join our community
            </a>
          </Button>
        </div>

        <!-- Stated plainly rather than sold: this was a glowing badge that said the same thing
             twice, in the tone of the thing it disclaims. The claim is scoped to voices on
             purpose - the project does use AI elsewhere, for code and the like, so an unqualified
             "never AI" would not be true. The waveform is the page's mark for a spoken line,
             standing here for who performed it. -->
        <p class="mt-7 flex max-w-xl flex-wrap items-center gap-x-2.5 gap-y-1 text-white/85">
          <VoiceMark class="h-3.5 w-auto shrink-0 text-[#fbd057]" />
          <span>100% human voice acting.</span>
          <RouterLink
            :to="{ name: 'faq', hash: '#ai-voices' }"
            class="text-[#fbd057] underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#fbd057]"
          >
            We will never use AI for voices.
          </RouterLink>
        </p>
      </div>

      <div class="lg:pl-4">
        <p class="mb-3 text-xs uppercase tracking-[0.3em] text-white/60">Trailer</p>
        <VideoEmbed video-id="7dAte6W6Eps" label="Play the Voices of Wynn trailer" />
      </div>
    </div>
  </section>

  <!-- The rest of the site -->
  <section class="border-y border-[#a340c4]/15 bg-[#faf6fd]">
    <div class="mx-auto max-w-6xl px-4 py-16 sm:px-6 md:py-20">
      <h2 class="flex items-center gap-3 text-2xl text-[#2a1438] sm:text-3xl">
        <VoiceMark class="h-3.5 w-auto shrink-0 text-[#a340c4]" />
        <span class="font-display">More to explore</span>
      </h2>
      <p class="mt-4 max-w-2xl text-lg leading-relaxed text-[#2a1438]/75">
        Browse who voiced what, read up on how the mod works, or meet the people behind it.
      </p>

      <ul class="mt-10 grid gap-6 md:grid-cols-3">
        <li
          v-for="page in readyPages"
          :key="page.title"
          class="flex flex-col rounded-xl border border-[#2a1438]/10 bg-white p-6 shadow-sm"
        >
          <span
            class="flex size-11 items-center justify-center rounded-lg bg-[#a340c4]/12 text-[#7b1a9b]"
          >
            <component :is="page.icon" class="size-5" aria-hidden="true" />
          </span>
          <h3 class="mt-5 font-display text-lg text-[#2a1438]">{{ page.title }}</h3>
          <p class="mt-3 flex-1 leading-relaxed text-[#2a1438]/70">{{ page.description }}</p>
          <Button as-child variant="brand" class="mt-6 w-fit">
            <RouterLink :to="page.to">
              <component :is="page.icon" class="size-4" aria-hidden="true" />
              {{ page.action }}
            </RouterLink>
          </Button>
        </li>
      </ul>
    </div>
  </section>

  <!-- Community, Patreon, contributors -->
  <section class="bg-white">
    <div class="mx-auto max-w-6xl px-4 py-16 sm:px-6 md:py-20">
      <p class="flex items-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]">
        <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
        Get involved
      </p>
      <h2 class="mt-4 font-display text-2xl text-[#2a1438] sm:text-3xl">Cast and crew</h2>

      <div class="mt-10 space-y-6">
        <article
          class="flex flex-col gap-6 rounded-2xl bg-[#2e1a47] p-8 text-white shadow-[0_18px_50px_rgba(46,26,71,0.25)] md:flex-row md:items-center md:justify-between md:p-10"
        >
          <div class="flex flex-col gap-5 sm:flex-row sm:items-start">
            <img src="/images/discord.png" alt="" class="size-12 shrink-0 rounded-lg" />
            <div>
              <h3 class="font-display text-xl">Support and community</h3>
              <p class="mt-3 max-w-xl text-lg leading-relaxed text-white/80">
                Need some help? Want to report a bug or suggest a feature? Our staff and community
                are always willing to assist.
              </p>
            </div>
          </div>
          <Button as-child variant="brand" size="lg" class="shrink-0">
            <a :href="DISCORD_URL" target="_blank" rel="noopener">
              <img src="/images/discord.png" alt="" class="size-5 rounded-sm" />
              Join our community
            </a>
          </Button>
        </article>

        <div class="grid gap-6 md:grid-cols-2">
          <article
            class="flex flex-col rounded-2xl border border-[#2a1438]/10 bg-white p-8 shadow-sm"
          >
            <span
              class="flex size-11 items-center justify-center rounded-lg bg-[#fbd057]/25 text-[#7b1a9b]"
            >
              <Heart class="size-5" aria-hidden="true" />
            </span>
            <h3 class="mt-5 font-display text-lg text-[#2a1438]">Show some love</h3>
            <p class="mt-3 flex-1 leading-relaxed text-[#2a1438]/70">
              Would you like to help keep this mod running and support the team? Feel free to become
              one of our spectacular Patrons!
            </p>
            <Button as-child variant="outline" class="mt-6 w-fit self-start">
              <a :href="PATREON_URL" target="_blank" rel="noopener">
                <img src="/images/patreon.png" alt="" class="size-5 rounded-sm" />
                Become a Patron
              </a>
            </Button>
          </article>

          <article
            class="flex flex-col rounded-2xl border border-[#2a1438]/10 bg-white p-8 shadow-sm"
          >
            <span
              class="flex size-11 items-center justify-center rounded-lg bg-[#a340c4]/12 text-[#7b1a9b]"
            >
              <component
                :is="auth.isAuthenticated ? User : LogIn"
                class="size-5"
                aria-hidden="true"
              />
            </span>
            <h3 class="mt-5 font-display text-lg text-[#2a1438]">Contributors</h3>
            <p class="mt-3 flex-1 leading-relaxed text-[#2a1438]/70">
              Are you a contributor? Log in to edit your profile.
            </p>
            <Button as-child variant="brand" class="mt-6 w-fit self-start">
              <RouterLink :to="auth.isAuthenticated ? { name: 'profile' } : { name: 'login' }">
                <component
                  :is="auth.isAuthenticated ? User : LogIn"
                  class="size-4"
                  aria-hidden="true"
                />
                {{ auth.isAuthenticated ? 'My account' : 'Login' }}
              </RouterLink>
            </Button>
          </article>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.hero {
  position: relative;
  background: linear-gradient(160deg, #3b2159 0%, #2e1a47 45%, #1b0f2d 100%);
}

/* The block artwork stays a texture, not a picture: faint and softened so white
   copy keeps its contrast against the flat purple underneath. */
.hero::before {
  content: '';
  position: absolute;
  inset: 0;
  background: url('/images/web_back.webp') center / cover no-repeat;
  opacity: 0.12;
  filter: blur(2px);
  pointer-events: none;
}

.hero::after {
  content: '';
  position: absolute;
  inset-inline: 0;
  bottom: 0;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(251, 208, 87, 0.55), transparent);
}

.hero-content {
  position: relative;
  z-index: 1;
  animation: hero-rise 0.6s ease-out both;
}

@keyframes hero-rise {
  from {
    opacity: 0;
    transform: translateY(12px);
  }
}

@media (prefers-reduced-motion: reduce) {
  .hero-content {
    animation: none;
  }
}
</style>
