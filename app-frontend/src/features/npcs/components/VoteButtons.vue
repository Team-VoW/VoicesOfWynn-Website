<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { MessageSquare, ThumbsDown, ThumbsUp } from 'lucide-vue-next'
import { toast } from 'vue-sonner'
import type { VoteType } from '@/api/types'
import { useNpcVote } from '../queries'

const props = defineProps<{
  npcId: number
  npcName: string
  upvotes: number
  downvotes: number
  commentCount: number
  myVote: VoteType | null
}>()

const emit = defineEmits<{ (e: 'open-comments'): void }>()

const counts = ref({ upvotes: props.upvotes, downvotes: props.downvotes })
const vote = ref<VoteType | null>(props.myVote)

// The caller's votes arrive after the profile does, so adopt them when they land.
watch(
  () => props.myVote,
  (value) => (vote.value = value),
)
watch(
  () => [props.upvotes, props.downvotes],
  ([up, down]) => (counts.value = { upvotes: up!, downvotes: down! }),
)

const mutation = useNpcVote()

async function cast(next: VoteType) {
  // Voting the same way twice withdraws the vote, as on the legacy site.
  const target = vote.value === next ? null : next
  const previous = { vote: vote.value, counts: { ...counts.value } }

  vote.value = target
  counts.value = {
    upvotes: counts.value.upvotes + delta('Up', previous.vote, target),
    downvotes: counts.value.downvotes + delta('Down', previous.vote, target),
  }

  try {
    const result = await mutation.mutateAsync({ npcId: props.npcId, vote: target })
    counts.value = { upvotes: result.upvotes, downvotes: result.downvotes }
    vote.value = result.myVote
  } catch {
    vote.value = previous.vote
    counts.value = previous.counts
    toast.error(`Your vote on ${props.npcName} could not be saved.`)
  }
}

function delta(type: VoteType, from: VoteType | null, to: VoteType | null) {
  return (to === type ? 1 : 0) - (from === type ? 1 : 0)
}

const upLabel = computed(() =>
  vote.value === 'Up' ? `Remove your upvote from ${props.npcName}` : `Upvote ${props.npcName}`,
)
const downLabel = computed(() =>
  vote.value === 'Down'
    ? `Remove your downvote from ${props.npcName}`
    : `Downvote ${props.npcName}`,
)
</script>

<template>
  <div class="flex items-center gap-1.5">
    <button
      type="button"
      class="vote-btn"
      :class="vote === 'Up' ? 'vote-btn--up-active' : ''"
      :aria-label="upLabel"
      :aria-pressed="vote === 'Up'"
      @click.stop="cast('Up')"
    >
      <ThumbsUp class="size-4" aria-hidden="true" />
      <span class="tabular-nums">{{ counts.upvotes }}</span>
    </button>

    <button
      type="button"
      class="vote-btn"
      :class="vote === 'Down' ? 'vote-btn--down-active' : ''"
      :aria-label="downLabel"
      :aria-pressed="vote === 'Down'"
      @click.stop="cast('Down')"
    >
      <ThumbsDown class="size-4" aria-hidden="true" />
      <span class="tabular-nums">{{ counts.downvotes }}</span>
    </button>

    <button
      type="button"
      class="vote-btn"
      :aria-label="`Read comments on ${npcName}`"
      @click.stop="emit('open-comments')"
    >
      <MessageSquare class="size-4" aria-hidden="true" />
      <span class="tabular-nums">{{ commentCount }}</span>
    </button>
  </div>
</template>

<style scoped>
.vote-btn {
  display: inline-flex;
  cursor: pointer;
  align-items: center;
  gap: 0.375rem;
  border-radius: 0.5rem;
  padding: 0.375rem 0.625rem;
  font-size: 0.875rem;
  color: rgba(42, 20, 56, 0.7);
  transition:
    background-color 0.2s ease,
    color 0.2s ease;
}

.vote-btn:hover {
  background-color: rgba(163, 64, 196, 0.1);
  color: #7b1a9b;
}

.vote-btn:focus-visible {
  outline: 2px solid #a340c4;
  outline-offset: 2px;
}

.vote-btn--up-active {
  background-color: rgba(52, 168, 83, 0.14);
  color: #1e7a36;
}

.vote-btn--down-active {
  background-color: rgba(217, 48, 37, 0.12);
  color: #b3261e;
}
</style>
