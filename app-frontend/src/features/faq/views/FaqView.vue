<script setup lang="ts">
import { nextTick, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { AccordionRoot } from 'reka-ui'
import { MessageCircle } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import VoiceMark from '@/features/home/components/VoiceMark.vue'
import FaqItem from '../components/FaqItem.vue'

const DISCORD_URL = 'https://discord.gg/kuEK3XH4Y5'
const SHADY_DISCORD_URL = 'https://discord.com/users/518828923186970646'

const route = useRoute()

const open = ref<string[]>([])

/**
 * Opens the question a #hash addresses and brings it into view. Links to /faq#data-processing
 * are handed out by the mod and by the old download pages, and they must still land on the
 * answer rather than on a collapsed page.
 */
async function openFromHash() {
  const id = route.hash.replace(/^#/, '')
  if (!id) return
  if (!open.value.includes(id)) open.value = [...open.value, id]

  // The panel has to expand before the browser can measure where to scroll to.
  await nextTick()
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

onMounted(openFromHash)
watch(() => route.hash, openFromHash)
</script>

<template>
  <section class="border-b border-[#a340c4]/15 bg-[#faf6fd]">
    <div class="mx-auto max-w-6xl px-4 py-14 text-center sm:px-6 md:py-16">
      <p
        class="flex items-center justify-center gap-3 text-xs uppercase tracking-[0.3em] text-[#7b1a9b]"
      >
        <VoiceMark class="h-3.5 w-auto text-[#a340c4]" />
        Questions, answered
      </p>
      <h1 class="mt-4 font-display text-2xl text-[#2a1438] sm:text-3xl">FAQ</h1>
      <p class="mx-auto mt-4 max-w-2xl text-lg leading-relaxed text-[#2a1438]/75">
        Do you have a question about the mod? You will most likely find it here.
      </p>
    </div>
  </section>

  <section class="bg-white">
    <div class="mx-auto max-w-4xl px-4 py-14 sm:px-6 md:py-16">
      <AccordionRoot
        v-model="open"
        type="multiple"
        class="rounded-2xl border border-[#2a1438]/10 bg-white px-5 shadow-sm sm:px-7"
      >
        <FaqItem id="voiced-characters" question="How many characters are voiced in the mod?">
          <p>
            Currently, most quest NPCs and dungeon NPCs are voiced. A handful of quests that
            Wynncraft has since changed the dialogue of are not complete at the moment. We’re
            working hard to bring those back up to date while adding voicelines to other NPCs as
            well.
          </p>
          <p>
            In addition to quest and dungeon NPCs, we’ve also added full voice acting for the
            Seaskipper Captain and the Talking Mushroom. Other significant NPCs will be added soon!
          </p>
        </FaqItem>

        <FaqItem id="ai-voices" question="Do you allow AI voices in this project?">
          <p><b>No.</b></p>
          <p>
            AI is the antithesis of creativity. We will never use AI to voice in this project. We
            always use real human performances in our work, even if it’s imperfect.
          </p>
          <p>We use 100% real human voice actors in our work.</p>
          <p>
            Outside of voicing, we use AI to assist us with some coding and repetitive management
            tasks.
          </p>
        </FaqItem>

        <FaqItem
          id="minecraft-version"
          question="What Minecraft version is needed for the mod to work?"
        >
          <p>The mod is currently developed for Fabric (Minecraft version <code>1.21.11</code>).</p>
          <p>
            We stay on whatever Minecraft version Wynntils uses, so Voices of Wynn is always
            compatible with it.
          </p>
        </FaqItem>

        <FaqItem
          id="playback-delay"
          question="Random sounds have a long playback delay, or the mod feels laggy in general."
        >
          <p>
            Open the mod’s settings (this requires the ModMenu mod) and enable
            <code>downloadSounds</code>. Restart your game, and all the audio will be downloaded and
            played from your local machine instead of being streamed.
          </p>
        </FaqItem>

        <FaqItem
          id="villager-sounds"
          question="The villager sounds that play when speaking to NPCs are so annoying! How do I turn them off?"
        >
          <p>
            Open the mod’s settings in ModMenu and disable village sounds. While you are there, you
            can also enable features such as auto continue.
          </p>
        </FaqItem>

        <FaqItem
          id="data-processing"
          question="What user data does the mod collect and how are they processed?"
        >
          <p>
            When you log in to Wynncraft with the mod enabled, it sends a request to our servers to
            check whether there’s an update available and also to log some things for analytical
            purposes.
          </p>
          <p>
            The main information sent to our servers with this request is a <code>sha256</code> hash
            of your player UUID. This hash can never be turned back into your UUID and serves as an
            anonymous and unique identificator of a user.
          </p>
          <p>
            Because of how HTTP requests work, your IP (or IP of any VPN or Tor exit node used) is
            also visible to us. This information is also hashed using the same algorithm
            (server-side this time).
          </p>
          <p>
            These two pieces of information are stored securely in our database. The hash of your IP
            address is deleted after 2 days at most, the hash of your UUID is kept permanently, but
            after two days at most, the timestamp when it was submitted is deleted.
          </p>
          <p>This image probably does a better job at explaining the flow of the whole process:</p>
          <img
            src="/images/data-processing.svg"
            alt="Flowchart: the mod hashes your UUID and sends it with an update check; the server hashes your IP, stores both, then deletes the IP hash and the timestamp within two days."
            class="mt-3 w-full rounded-xl border border-[#2a1438]/10 bg-white p-3"
            loading="lazy"
          />
          <p>In the end, we’re left with the following statistics:</p>
          <ol>
            <li>How many players used our mod on any given day.</li>
            <li>The total number of unique players who used our mod at least once.</li>
          </ol>
          <p>
            The hash of your UUID is kept to not count you into the second statistic more than once.
          </p>
        </FaqItem>

        <FaqItem id="can-i-voice" question="Can I voice a character?">
          <p>
            Of course, we’d be more than happy to have another voice actor. Simply
            <a :href="DISCORD_URL" target="_blank" rel="noopener">join our Discord</a> and we’ll
            guide you from there.
          </p>
        </FaqItem>

        <FaqItem id="voice-doesnt-fit" question="I think that a certain NPC’s voice doesn’t fit.">
          <p>
            We’re always appreciative of feedback. Some roles were cast early in the mod, when we
            had much lower standards for performances. One way to signal which NPCs need revoicing
            is to vote for the recordings in
            <RouterLink :to="{ name: 'contents' }">Mod Contents</RouterLink>. Click the name of any
            NPC and navigate down to the recording of your choice. There, you can also leave
            comments with feedback for the voice actors. Keep in mind that, if you are not logged
            into the site, your IP address will be stored to prevent abuse.
          </p>
        </FaqItem>

        <FaqItem
          id="missing-dialogue"
          question="Some dialogue isn’t playing. Where can I report it?"
        >
          <p>
            Be aware that some dialogue, especially dialogue said by most of the white-named NPCs
            (non-quest NPCs) is not voiced. However, all dialogue related to quests and dungeons
            should be voiced. If you enabled line reporting after installing Voices of Wynn, you
            don’t need to do anything, because the reports will be sent automatically.
          </p>
          <p>
            If you run into a different issue,
            <a :href="DISCORD_URL" target="_blank" rel="noopener">join our Discord server</a> and
            navigate down to <b class="whitespace-nowrap">#🙋issues-and-support</b>.
          </p>
        </FaqItem>

        <FaqItem id="create-account" question="I saw a login button. How can I create an account?">
          <p>
            The ability to create accounts is reserved for system administrators. Only contributors
            – voice actors, coders, or script writers – can have an account. If you apply and are
            picked for a role, then administrators will create an account for you.
          </p>
        </FaqItem>

        <FaqItem
          id="forgot-password"
          question="I have an account, but forgot my password. There’s no “forgot password” button, so what do I do?"
        >
          <p>
            If you forgot your password, contact
            <a :href="SHADY_DISCORD_URL" target="_blank" rel="noopener">@shady_medic</a> on Discord
            and explain. It’ll be easier if you contact him from the same account you used to apply
            to the project. Shady will reset your password.
          </p>
        </FaqItem>

        <FaqItem
          id="report-content"
          question="I found offensive, insulting, or inappropriate content on this webpage. Where do I report it?"
        >
          <p>
            Currently, there is no report option. Contact
            <a :href="SHADY_DISCORD_URL" target="_blank" rel="noopener">@shady_medic</a> on Discord
            to report inappropriate content.
          </p>
        </FaqItem>

        <FaqItem
          id="contact-without-discord"
          question="I need to contact an administrator, but I don’t have nor want to create a Discord account. What should I do?"
        >
          <p>
            You may contact us through
            <a href="mailto:team@voicesofwynn.com">our email inbox</a>. Keep in mind that this email
            is not checked as regularly as Discord.
          </p>
        </FaqItem>
      </AccordionRoot>

      <div
        class="mt-10 flex flex-wrap items-center justify-between gap-x-6 gap-y-4 rounded-2xl border border-[#2a1438]/10 bg-[#faf6fd] p-6"
      >
        <div>
          <h2 class="font-display text-base text-[#2a1438]">Still stuck?</h2>
          <p class="mt-1.5 text-[#2a1438]/70">
            Ask in the Discord server. Someone is usually around.
          </p>
        </div>
        <Button as-child variant="brand">
          <a :href="DISCORD_URL" target="_blank" rel="noopener">
            <MessageCircle class="size-4" aria-hidden="true" />
            Join the Discord
          </a>
        </Button>
      </div>
    </div>
  </section>
</template>
