<script setup lang="ts">
import { ref, watch } from 'vue'
import { Check, MessageSquarePlus } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import SeekableAudioPlayer from '@/components/audio/SeekableAudioPlayer.vue'
import type { CastingAudition } from '@/api/types'

/**
 * `vote` casts a vote with an optional comment; `comment` only writes the comment, which works
 * whether or not the audition is picked.
 */
export type AuditionEditMode = 'vote' | 'comment'

const props = defineProps<{
  audition: CastingAudition
  /** Voting is open and the voter has not marked the character done. */
  canEdit: boolean
  editing: AuditionEditMode | null
  saving: boolean
}>()

const emit = defineEmits<{
  edit: [mode: AuditionEditMode]
  cancel: []
  submit: [mode: AuditionEditMode, comment: string]
  unvote: []
  deleteComment: []
}>()

const draft = ref('')
const commentsOpen = ref(false)

watch(
  () => props.editing,
  (editing) => {
    if (editing) draft.value = props.audition.myComment ?? ''
  },
  { immediate: true },
)
</script>

<template>
  <li
    class="border-b last:border-b-0"
    :class="audition.myVote || editing ? 'bg-primary/[0.03]' : ''"
    :data-testid="`audition-${audition.id}`"
  >
    <!-- Mobile: number, name and vote on one line with the player full-width beneath. sm+: table columns. -->
    <div
      class="grid grid-cols-[1.5rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 px-4 py-3 sm:grid-cols-[2rem_minmax(7rem,1fr)_minmax(10rem,1.6fr)_6.5rem] sm:gap-y-0 sm:py-2.5"
    >
      <span class="text-sm text-muted-foreground tabular-nums">{{ audition.number }}</span>
      <div class="min-w-0">
        <p class="truncate text-sm font-medium">{{ audition.auditioneeName }}</p>
        <div class="flex flex-wrap gap-x-2 text-xs">
          <button
            v-if="audition.anonymousComments.length > 0"
            type="button"
            class="cursor-pointer text-primary hover:underline"
            :aria-expanded="commentsOpen"
            @click="commentsOpen = !commentsOpen"
          >
            {{ commentsOpen ? 'Hide' : '' }} {{ audition.anonymousComments.length }} anonymous
            comment{{ audition.anonymousComments.length === 1 ? '' : 's' }}
          </button>
          <button
            v-if="canEdit && !editing && !audition.myVote && !audition.myComment"
            type="button"
            class="inline-flex cursor-pointer items-center gap-1 text-muted-foreground hover:text-primary"
            @click="emit('edit', 'comment')"
          >
            <MessageSquarePlus class="size-3.5" />
            Comment
          </button>
        </div>
      </div>
      <SeekableAudioPlayer
        class="order-last col-span-full sm:order-none sm:col-span-1"
        :src="audition.audioUrl"
        :label="`audition ${audition.number} by ${audition.auditioneeName}`"
        :duration-seconds="audition.durationSeconds"
      />
      <div class="flex justify-end">
        <Button
          v-if="audition.myVote"
          type="button"
          size="sm"
          class="w-26"
          :disabled="!canEdit"
          :title="canEdit ? 'Remove vote (your comment is kept)' : undefined"
          @click="emit('unvote')"
        >
          <Check class="size-4" />
          Voted
        </Button>
        <Button
          v-else-if="canEdit && editing !== 'vote'"
          type="button"
          size="sm"
          variant="outline"
          class="w-26 border-primary/30 text-primary hover:bg-primary/5"
          @click="emit('edit', 'vote')"
        >
          Vote
        </Button>
      </div>
    </div>

    <form
      v-if="editing"
      class="space-y-2 px-4 pb-4 sm:pl-[3.25rem]"
      @submit.prevent="emit('submit', editing, draft)"
    >
      <label :for="`comment-${audition.id}`" class="text-sm font-medium">
        Comment
        <span class="font-normal text-muted-foreground">
          {{ editing === 'vote' ? 'Optional. ' : '' }}Other staff see it anonymously.
        </span>
      </label>
      <textarea
        :id="`comment-${audition.id}`"
        v-model="draft"
        rows="2"
        maxlength="1000"
        placeholder="e.g. Nails the tired warmth in the direction, clean mic, strong last line"
        class="flex w-full resize-y rounded-md border border-primary/25 bg-background px-3 py-2 text-sm shadow-xs outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
      />
      <div class="flex gap-2">
        <Button
          type="submit"
          size="sm"
          :disabled="saving || (editing === 'comment' && !draft.trim() && !audition.myComment)"
        >
          {{ editing === 'vote' ? 'Cast vote' : 'Save comment' }}
        </Button>
        <Button type="button" size="sm" variant="outline" @click="emit('cancel')">Cancel</Button>
      </div>
    </form>

    <div
      v-else-if="audition.myVote || audition.myComment"
      class="flex flex-wrap items-baseline gap-x-3 gap-y-1 px-4 pb-3 text-sm sm:flex-nowrap sm:pl-[3.25rem]"
    >
      <span class="shrink-0 font-medium text-primary">
        Your comment<span v-if="!audition.myVote" class="font-normal text-muted-foreground"> (no vote)</span>
      </span>
      <span class="min-w-0 basis-full break-words sm:basis-auto sm:flex-1" :class="audition.myComment ? '' : 'text-muted-foreground'">
        {{ audition.myComment || 'No comment added.' }}
      </span>
      <span v-if="canEdit" class="flex shrink-0 gap-3 text-xs">
        <button type="button" class="cursor-pointer text-primary hover:underline" @click="emit('edit', 'comment')">
          {{ audition.myComment ? 'Edit' : 'Add comment' }}
        </button>
        <button
          v-if="audition.myComment"
          type="button"
          class="cursor-pointer text-muted-foreground hover:text-destructive"
          @click="emit('deleteComment')"
        >
          Delete
        </button>
      </span>
    </div>

    <ul
      v-if="commentsOpen && audition.anonymousComments.length > 0"
      class="space-y-1.5 px-4 pb-3.5 sm:pl-[3.25rem]"
    >
      <li
        v-for="(comment, index) in audition.anonymousComments"
        :key="index"
        class="flex gap-3 rounded-md bg-primary/[0.04] px-3 py-2 text-sm"
      >
        <span class="shrink-0 text-muted-foreground">Anonymous</span>
        <span class="min-w-0 break-words">{{ comment }}</span>
      </li>
    </ul>
  </li>
</template>
